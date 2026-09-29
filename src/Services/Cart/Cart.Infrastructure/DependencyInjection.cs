using Cart.Application.Carts;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Cart.Infrastructure.Catalog;
using Cart.Infrastructure.Redis;
using Catalog.Contracts.Grpc.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using StackExchange.Redis;

namespace Cart.Infrastructure;

/// <summary>Registers Redis cart persistence and resilient internal Catalog gRPC.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Cart infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCartInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' was not configured.");
        string catalogAddress = configuration["Grpc:Catalog"]
            ?? throw new InvalidOperationException("Grpc:Catalog was not configured.");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<ICartStore, RedisCartStore>();
        services.AddScoped<CartService>();
        services.AddGrpcClient<CatalogInternalGrpc.CatalogInternalGrpcClient>(options => options.Address = new Uri(catalogAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(100);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
            });
        services.AddScoped<ICatalogProductClient, CatalogProductClient>();
        services.AddRedisReadiness();
        return services;
    }
}
