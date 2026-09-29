using Catalog.Domain.Categories;
using Catalog.Domain.Products;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Represents the Catalog service database and its aggregate persistence mappings.
/// </summary>
public sealed class CatalogDbContext : DomainEventsDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDbContext"/> class.
    /// </summary>
    /// <param name="options">Options that configure the Catalog database provider.</param>
    /// <param name="publisher">Publisher used to dispatch aggregate domain events.</param>
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    /// <summary>
    /// Gets the products tracked by the Catalog service.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Gets categories owned by the Catalog service.</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Gets transactional integration messages awaiting publication.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Gets successfully handled integration messages.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>
    /// Applies all Catalog entity configurations from the infrastructure assembly.
    /// </summary>
    /// <param name="modelBuilder">Builder used to construct the Catalog persistence model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.ConfigureCommerceMessaging();

        base.OnModelCreating(modelBuilder);
    }
}
