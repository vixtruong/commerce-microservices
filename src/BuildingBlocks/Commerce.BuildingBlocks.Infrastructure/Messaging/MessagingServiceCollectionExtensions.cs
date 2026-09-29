using Commerce.BuildingBlocks.Contracts.Messaging;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Registers RabbitMQ publication, durable subscriptions and transactional outbox processing.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>Adds the singleton RabbitMQ event bus.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }

    /// <summary>Adds one durable typed RabbitMQ subscription.</summary>
    /// <typeparam name="TEvent">Integration event contract.</typeparam>
    /// <typeparam name="THandler">Scoped handler implementation.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="queueName">Durable service-owned queue.</param>
    /// <param name="routingKey">Topic routing key.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, THandler>(
        this IServiceCollection services,
        string queueName,
        string routingKey)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.Configure<RabbitMqSubscriptionOptions<TEvent>>(options =>
        {
            options.QueueName = queueName;
            options.RoutingKey = routingKey;
        });
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddHostedService<RabbitMqConsumerHostedService<TEvent>>();
        return services;
    }

    /// <summary>Adds a multi-instance-safe outbox processor for the service context.</summary>
    /// <typeparam name="TDbContext">Service database context.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOutboxProcessor<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddHostedService<OutboxProcessor<TDbContext>>();
        return services;
    }

    /// <summary>Adds scoped outbox and inbox ports backed by the service context.</summary>
    /// <typeparam name="TDbContext">Service database context.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTransactionalMessaging<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<EfTransactionalMessaging<TDbContext>>();
        services.AddScoped<IOutbox>(provider => provider.GetRequiredService<EfTransactionalMessaging<TDbContext>>());
        services.AddScoped<IInbox>(provider => provider.GetRequiredService<EfTransactionalMessaging<TDbContext>>());
        return services;
    }
}
