using Catalog.Application.Abstractions;
using Catalog.Application.Categories;
using Catalog.Infrastructure.Caching;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Catalog.Infrastructure;

/// <summary>
/// Registers Catalog infrastructure services.
/// </summary>
public static class DependencyInjection
{
    private const string ConnectionStringName = "CatalogDb";

    /// <summary>
    /// Adds Catalog persistence and repository implementations.
    /// </summary>
    /// <param name="services">Dependency injection service collection.</param>
    /// <param name="configuration">
    /// Host configuration containing the Catalog connection string.
    /// </param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Catalog connection string is missing.
    /// </exception>
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' " +
                "was not configured.");

        services.AddDbContext<CatalogDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(
                        typeof(CatalogDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        string redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' was not configured.");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<
            IUnitOfWork,
            EfUnitOfWork<CatalogDbContext>>();

        services.AddScoped<
            IProductRepository,
            ProductRepository>();
        services.AddSingleton<IProductCache, RedisProductCache>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddPostgresReadiness<CatalogDbContext>();
        services.AddRedisReadiness();

        return services;
    }
}
