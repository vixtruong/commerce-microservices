using System.Diagnostics;
using System.Text.Json;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Commerce.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Configures a durable queue subscription for one integration event contract.
/// </summary>
/// <typeparam name="TEvent">Subscribed event type.</typeparam>
public sealed class RabbitMqSubscriptionOptions<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Gets or sets the durable queue name.</summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>Gets or sets the topic binding routing key.</summary>
    public string RoutingKey { get; set; } = string.Empty;
}

/// <summary>
/// Consumes one versioned event contract with manual acknowledgement, bounded retry and dead lettering.
/// </summary>
/// <typeparam name="TEvent">Integration event contract.</typeparam>
public sealed class RabbitMqConsumerHostedService<TEvent> : BackgroundService
    where TEvent : IIntegrationEvent
{
    private static readonly ActivitySource ActivitySource = new("Commerce.Messaging");
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _brokerOptions;
    private readonly RabbitMqSubscriptionOptions<TEvent> _subscription;
    private readonly ILogger<RabbitMqConsumerHostedService<TEvent>> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    /// <summary>Initializes a RabbitMQ consumer.</summary>
    /// <param name="scopeFactory">Factory used to resolve one scoped handler per delivery.</param>
    /// <param name="brokerOptions">Shared broker options.</param>
    /// <param name="subscriptionOptions">Queue and routing-key options.</param>
    /// <param name="logger">Structured logger.</param>
    public RabbitMqConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> brokerOptions,
        IOptions<RabbitMqSubscriptionOptions<TEvent>> subscriptionOptions,
        ILogger<RabbitMqConsumerHostedService<TEvent>> logger)
    {
        _scopeFactory = scopeFactory;
        _brokerOptions = brokerOptions.Value;
        _subscription = subscriptionOptions.Value;
        _logger = logger;
    }

    /// <summary>Opens the durable subscription and consumes until shutdown.</summary>
    /// <param name="stoppingToken">Application shutdown token.</param>
    /// <returns>A task representing the consumer lifetime.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_brokerOptions.ConnectionString),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        DeclareTopology(_channel);
        _channel.BasicQos(0, 16, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleDeliveryAsync;
        _channel.BasicConsume(_subscription.QueueName, autoAck: false, consumer);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    /// <summary>Stops consumption and disposes broker resources.</summary>
    /// <param name="cancellationToken">Shutdown token.</param>
    /// <returns>A task that completes after cleanup.</returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    /// <summary>Declares the main, retry and dead-letter topology.</summary>
    /// <param name="channel">Open broker channel.</param>
    private void DeclareTopology(IModel channel)
    {
        string retryQueue = $"{_subscription.QueueName}.retry";
        channel.ExchangeDeclare(_brokerOptions.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ExchangeDeclare(_brokerOptions.DeadLetterExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.QueueDeclare(_subscription.QueueName, durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = _brokerOptions.DeadLetterExchangeName
            });
        channel.QueueBind(_subscription.QueueName, _brokerOptions.ExchangeName, _subscription.RoutingKey);

        // Failed deliveries wait briefly before RabbitMQ routes them back to the main exchange.
        channel.QueueDeclare(retryQueue, durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = _brokerOptions.ExchangeName,
                ["x-dead-letter-routing-key"] = _subscription.RoutingKey
            });
        channel.QueueDeclare($"{_subscription.QueueName}.dead", durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind($"{_subscription.QueueName}.dead", _brokerOptions.DeadLetterExchangeName, "#");
    }

    /// <summary>Deserializes and dispatches one delivery before acknowledging it.</summary>
    /// <param name="sender">RabbitMQ consumer instance.</param>
    /// <param name="delivery">Delivery metadata and body.</param>
    /// <returns>A task that completes after acknowledgement or retry routing.</returns>
    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs delivery)
    {
        if (_channel is null)
        {
            return;
        }

        ActivityContext parentContext = ExtractParentContext(delivery.BasicProperties.Headers);
        using Activity? activity = ActivitySource.StartActivity(
            $"rabbitmq consume {_subscription.RoutingKey}", ActivityKind.Consumer, parentContext);
        try
        {
            TEvent integrationEvent = JsonSerializer.Deserialize<TEvent>(delivery.Body.Span)
                ?? throw new JsonException($"Could not deserialize {typeof(TEvent).Name}.");
            activity?.SetTag("messaging.message.id", integrationEvent.MessageId);
            activity?.SetTag("messaging.destination.name", _subscription.QueueName);

            using IServiceScope scope = _scopeFactory.CreateScope();
            IIntegrationEventHandler<TEvent> handler =
                scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TEvent>>();
            await handler.HandleAsync(integrationEvent, CancellationToken.None);
            _channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            int retryCount = ReadRetryCount(delivery.BasicProperties.Headers) + 1;
            _logger.LogError(exception,
                "RabbitMQ handler failed for {EventType} on attempt {RetryCount}",
                typeof(TEvent).Name,
                retryCount);

            if (retryCount < _brokerOptions.MaxDeliveryAttempts)
            {
                IBasicProperties retryProperties = _channel.CreateBasicProperties();
                retryProperties.Persistent = true;
                retryProperties.MessageId = delivery.BasicProperties.MessageId;
                retryProperties.CorrelationId = delivery.BasicProperties.CorrelationId;
                retryProperties.Type = delivery.BasicProperties.Type;
                retryProperties.ContentType = delivery.BasicProperties.ContentType;
                retryProperties.Headers = new Dictionary<string, object> { ["x-retry-count"] = retryCount };
                _channel.BasicPublish(string.Empty, $"{_subscription.QueueName}.retry", retryProperties, delivery.Body);
                _channel.BasicAck(delivery.DeliveryTag, multiple: false);
            }
            else
            {
                _channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }

    /// <summary>Reads the bounded retry counter from broker headers.</summary>
    /// <param name="headers">Delivery headers.</param>
    /// <returns>The previous retry count.</returns>
    private static int ReadRetryCount(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out object? value))
        {
            return 0;
        }

        return value switch
        {
            int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out int parsed) => parsed,
            _ => 0
        };
    }

    /// <summary>Extracts W3C trace context propagated through RabbitMQ headers.</summary>
    /// <param name="headers">Broker message headers.</param>
    /// <returns>The remote parent context, or the default context when absent.</returns>
    private static ActivityContext ExtractParentContext(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("traceparent", out object? traceParentValue))
        {
            return default;
        }

        string? traceParent = traceParentValue switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string value => value,
            _ => null
        };
        string? traceState = headers.TryGetValue("tracestate", out object? traceStateValue)
            ? traceStateValue switch
            {
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                string value => value,
                _ => null
            }
            : null;

        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out ActivityContext context)
            ? context
            : default;
    }
}
