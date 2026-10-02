using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shipping.Application.Shipments;
using Shipping.Infrastructure.Persistence;

namespace Shipping.Infrastructure;

/// <summary>Registers Shipping persistence and reliable messaging.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Shipping infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddShippingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string database = configuration.GetConnectionString("ShippingDb")
            ?? throw new InvalidOperationException("Connection string 'ShippingDb' was not configured.");
        services.AddDbContext<ShippingDbContext>(options => options.UseNpgsql(database, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IShippingReadStore, ShippingReadStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<ShippingDbContext>>();
        services.AddScoped<ShippingService>();
        services.AddCommerceMessaging(configuration);
        services.AddTransactionalMessaging<ShippingDbContext>();
        services.AddOutboxProcessor<ShippingDbContext>();
        services.AddPostgresReadiness<ShippingDbContext>();
        services.AddRabbitMqReadiness();
        return services;
    }
}
