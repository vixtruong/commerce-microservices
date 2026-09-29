using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shipping.Domain.Shipments;

namespace Shipping.Infrastructure.Persistence;

/// <summary>Owns Shipping aggregates and reliable message state.</summary>
public sealed class ShippingDbContext : DbContext
{
    /// <summary>Initializes the Shipping context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options)
    {
    }

    /// <summary>Gets shipments.</summary>
    public DbSet<Shipment> Shipments => Set<Shipment>();
    /// <summary>Gets pending integration events.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    /// <summary>Gets handled integration messages.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Configures shipment snapshots, OrderId idempotency, Inbox, and Outbox.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("shipping");
        modelBuilder.Entity<Shipment>(builder =>
        {
            builder.ToTable("shipments");
            builder.HasKey(shipment => shipment.Id);
            builder.Property(shipment => shipment.Id).HasConversion(id => id.Value, value => new ShipmentId(value));
            builder.HasIndex(shipment => shipment.OrderId).IsUnique();
            builder.HasIndex(shipment => shipment.TrackingNumber).IsUnique();
            builder.Property(shipment => shipment.TrackingNumber).HasMaxLength(50);
            builder.Property(shipment => shipment.Status).HasConversion<string>().HasMaxLength(30);
            builder.Ignore(shipment => shipment.DomainEvents);
            builder.OwnsOne(shipment => shipment.DeliveryAddress, address =>
            {
                address.Property(value => value.RecipientName).HasColumnName("recipient_name").HasMaxLength(200);
                address.Property(value => value.Line1).HasColumnName("address_line_1").HasMaxLength(300);
                address.Property(value => value.City).HasColumnName("city").HasMaxLength(100);
                address.Property(value => value.PostalCode).HasColumnName("postal_code").HasMaxLength(30);
                address.Property(value => value.CountryCode).HasColumnName("country_code").HasMaxLength(2);
            });
        });
        modelBuilder.ConfigureCommerceMessaging();
    }
}
