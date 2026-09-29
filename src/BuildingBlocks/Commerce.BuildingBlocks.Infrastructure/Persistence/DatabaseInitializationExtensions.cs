using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Provides controlled database migration support for local and test hosts.
/// </summary>
public static class DatabaseInitializationExtensions
{
    /// <summary>
    /// Applies pending migrations for one service-owned database.
    /// </summary>
    /// <typeparam name="TDbContext">The service database context.</typeparam>
    /// <param name="services">The application service provider.</param>
    /// <param name="cancellationToken">Token used to cancel migration I/O.</param>
    /// <returns>A task that completes after all migrations are applied.</returns>
    public static async Task MigrateDatabaseAsync<TDbContext>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Startup migration is intentionally invoked only by Development hosts. Production
        // deployments should run the same checked-in migrations as a separate release step.
        await context.Database.MigrateAsync(cancellationToken);
    }
}
