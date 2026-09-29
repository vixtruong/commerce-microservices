using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Commerce.BuildingBlocks.Infrastructure.Health;

/// <summary>Registers readiness checks for service-owned dependencies.</summary>
public static class DependencyHealthCheckExtensions
{
    /// <summary>Adds a PostgreSQL connectivity readiness check through a service DbContext.</summary>
    /// <typeparam name="TDbContext">Service-owned Entity Framework context.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPostgresReadiness<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddHealthChecks().AddCheck<DbContextReadinessCheck<TDbContext>>("postgres", tags: ["ready"]);
        return services;
    }

    /// <summary>Adds a Redis ping readiness check.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRedisReadiness(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<RedisReadinessCheck>("redis", tags: ["ready"]);
        return services;
    }

    /// <summary>Adds a RabbitMQ connection readiness check.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRabbitMqReadiness(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<RabbitMqReadinessCheck>("rabbitmq", tags: ["ready"]);
        return services;
    }
}

/// <summary>Checks whether a service can reach its own relational database.</summary>
/// <typeparam name="TDbContext">Service-owned database context.</typeparam>
internal sealed class DbContextReadinessCheck<TDbContext> : IHealthCheck
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes the database readiness check.</summary>
    /// <param name="scopeFactory">Factory used to resolve the scoped context.</param>
    public DbContextReadinessCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext database = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The service database is unreachable.");
    }
}

/// <summary>Checks whether Redis responds to a bounded ping.</summary>
internal sealed class RedisReadinessCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Initializes the Redis readiness check.</summary>
    /// <param name="redis">Shared Redis connection.</param>
    public RedisReadinessCheck(IConnectionMultiplexer redis) => _redis = redis;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await _redis.GetDatabase().PingAsync();
        return HealthCheckResult.Healthy();
    }
}

/// <summary>Checks whether RabbitMQ accepts an authenticated connection.</summary>
internal sealed class RabbitMqReadinessCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;

    /// <summary>Initializes the RabbitMQ readiness check.</summary>
    /// <param name="options">Broker connection options.</param>
    public RabbitMqReadinessCheck(IOptions<RabbitMqOptions> options) => _options = options.Value;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString), RequestedConnectionTimeout = TimeSpan.FromSeconds(2) };
        using IConnection connection = factory.CreateConnection();
        return Task.FromResult(connection.IsOpen
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ did not open a connection."));
    }
}
