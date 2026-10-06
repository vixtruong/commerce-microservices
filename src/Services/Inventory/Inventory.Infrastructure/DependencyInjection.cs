using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Inventory.Application.Stock;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure;

/// <summary>Registers Inventory PostgreSQL persistence and reliable messaging.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Inventory infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("Connection string 'InventoryDb' was not configured.");
        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
            npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryReadStore, InventoryReadStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<InventoryDbContext>>();
        services.AddScoped<InventoryService>();
        services.AddScoped<ReservationExpirationService>();
        services.Configure<ReservationExpirationOptions>(configuration.GetSection("InventoryExpiration"));
        services.AddHostedService<ReservationExpirationWorker>();
        services.AddCommerceMessaging(configuration);
        services.AddTransactionalMessaging<InventoryDbContext>();
        services.AddOutboxProcessor<InventoryDbContext>();
        services.AddPostgresReadiness<InventoryDbContext>();
        services.AddRabbitMqReadiness();
        return services;
    }
}
