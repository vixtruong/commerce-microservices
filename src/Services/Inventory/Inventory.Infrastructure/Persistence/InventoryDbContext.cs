using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Inventory.Domain.Stock;
using Inventory.Application.Stock;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Owns Inventory stock, reservation, inbox, and outbox tables.</summary>
public sealed class InventoryDbContext : DbContext
{
    /// <summary>Initializes the Inventory context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    /// <summary>Gets stock aggregates.</summary>
    public DbSet<StockItem> StockItems => Set<StockItem>();
    /// <summary>Gets immutable stock-adjustment audit records.</summary>
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    /// <summary>Gets stock reservations.</summary>
    public DbSet<StockReservation> Reservations => Set<StockReservation>();
    /// <summary>Gets pending integration events.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    /// <summary>Gets handled broker messages.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Configures Inventory mappings and database-enforced idempotency.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.Entity<StockAdjustment>(builder =>
        {
            builder.ToTable("stock_adjustments");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Reason).HasMaxLength(500);
            builder.HasIndex(a => new { a.ProductId, a.CreatedAtUtc });
        });
        modelBuilder.Entity<StockItem>(builder =>
        {
            builder.ToTable("stock_items");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasConversion(id => id.Value, value => new StockItemId(value));
            builder.HasIndex(item => item.ProductId).IsUnique();
            builder.Property(item => item.Version).IsConcurrencyToken();
            builder.Ignore(item => item.AvailableQuantity);
            builder.Ignore(item => item.DomainEvents);
            builder.HasMany(item => item.Reservations).WithOne().HasForeignKey(reservation => reservation.StockItemId);
            builder.Navigation(item => item.Reservations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<StockReservation>(builder =>
        {
            builder.ToTable("stock_reservations");
            builder.HasKey(reservation => reservation.Id);
            builder.Property(reservation => reservation.Id).HasConversion(id => id.Value, value => new StockReservationId(value));
            builder.Property(reservation => reservation.StockItemId).HasConversion(id => id.Value, value => new StockItemId(value));
            builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(32);
            // This unique index is the durable idempotency guard for OrderId + ProductId.
            builder.HasIndex(reservation => new { reservation.OrderId, reservation.ProductId }).IsUnique();
            // The expiry worker uses this composite index for bounded pending-lease scans.
            builder.HasIndex(reservation => new { reservation.Status, reservation.ExpiresAtUtc });
        });
        modelBuilder.ConfigureCommerceMessaging();
    }
}
