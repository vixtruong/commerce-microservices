using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Notifications;
using Notification.Infrastructure.Delivery;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure;

/// <summary>Registers Notification durable ingestion, fake delivery, and RabbitMQ.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Notification infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Worker configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string database = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationDb' was not configured.");
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(database, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<IEmailSender, FakeEmailSender>();
        services.AddHostedService<NotificationDispatcher>();
        services.AddCommerceMessaging(configuration);
        services.AddPostgresReadiness<NotificationDbContext>();
        services.AddRabbitMqReadiness();
        return services;
    }
}
