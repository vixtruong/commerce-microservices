using Cart.Contracts.Grpc;
using Catalog.Contracts.Grpc.Products;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Checkout;
using Ordering.Infrastructure.Grpc;
using Ordering.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Ordering.Infrastructure;

/// <summary>Registers Ordering persistence, Redis idempotency, gRPC ports, and messaging.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Ordering infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOrderingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string database = configuration.GetConnectionString("OrderingDb")
            ?? throw new InvalidOperationException("Connection string 'OrderingDb' was not configured.");
        string redis = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' was not configured.");
        string cartAddress = configuration["Grpc:Cart"]
            ?? throw new InvalidOperationException("Grpc:Cart was not configured.");
        string catalogAddress = configuration["Grpc:Catalog"]
            ?? throw new InvalidOperationException("Grpc:Catalog was not configured.");

        services.AddDbContext<OrderingDbContext>(options => options.UseNpgsql(database, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<OrderingDbContext>>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddGrpcClient<CartInternalGrpc.CartInternalGrpcClient>(options => options.Address = new Uri(cartAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.Retry.MaxRetryAttempts = 2;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
            });
        services.AddGrpcClient<CatalogInternalGrpc.CatalogInternalGrpcClient>(options => options.Address = new Uri(catalogAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.Retry.MaxRetryAttempts = 2;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
            });
        services.AddScoped<ICartCheckoutClient, CartCheckoutClient>();
        services.AddScoped<ICatalogCheckoutClient, CatalogCheckoutClient>();
        services.AddCommerceMessaging(configuration);
        services.AddTransactionalMessaging<OrderingDbContext>();
        services.AddOutboxProcessor<OrderingDbContext>();
        services.AddPostgresReadiness<OrderingDbContext>();
        services.AddRedisReadiness();
        services.AddRabbitMqReadiness();
        return services;
    }
}
