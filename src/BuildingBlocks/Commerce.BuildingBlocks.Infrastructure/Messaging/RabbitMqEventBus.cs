using System.Diagnostics;
using System.Text;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Commerce.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Publishes persistent integration messages to a durable RabbitMQ topic exchange.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Commerce.Messaging");
    private readonly RabbitMqOptions _options;
    private readonly Lazy<IConnection> _connection;

    /// <summary>Initializes the RabbitMQ event bus.</summary>
    /// <param name="options">Broker connection and topology options.</param>
    public RabbitMqEventBus(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
        _connection = new Lazy<IConnection>(CreateConnection, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public Task PublishAsync(IntegrationMessageEnvelope message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Activity? activity = ActivitySource.StartActivity($"rabbitmq publish {message.Type}", ActivityKind.Producer);
        activity?.SetTag("messaging.message.id", message.MessageId);
        activity?.SetTag("messaging.destination.name", _options.ExchangeName);

        using IModel channel = _connection.Value.CreateModel();
        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ConfirmSelect();

        IBasicProperties properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.MessageId.ToString("D");
        properties.CorrelationId = message.CorrelationId.ToString("D");
        properties.Type = message.Type;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(message.OccurredOnUtc.ToUnixTimeSeconds());
        properties.Headers = new Dictionary<string, object>();
        if (Activity.Current?.Id is string traceParent)
        {
            properties.Headers["traceparent"] = traceParent;
        }

        if (Activity.Current?.TraceStateString is string traceState)
        {
            properties.Headers["tracestate"] = traceState;
        }
        if (message.CausationId.HasValue)
        {
            properties.Headers["causation-id"] = message.CausationId.Value.ToString("D");
        }

        byte[] body = Encoding.UTF8.GetBytes(message.Payload);
        channel.BasicPublish(_options.ExchangeName, message.Type, mandatory: true, properties, body);

        // Publisher confirms ensure an outbox row is not marked processed before the broker accepts it.
        if (!channel.WaitForConfirms(TimeSpan.FromSeconds(10)))
        {
            throw new InvalidOperationException($"RabbitMQ did not confirm message {message.MessageId}.");
        }

        return Task.CompletedTask;
    }

    /// <summary>Disposes the lazily-created broker connection.</summary>
    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }

    /// <summary>Creates an automatically recovering broker connection.</summary>
    /// <returns>An open RabbitMQ connection.</returns>
    private IConnection CreateConnection()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        return factory.CreateConnection();
    }
}
