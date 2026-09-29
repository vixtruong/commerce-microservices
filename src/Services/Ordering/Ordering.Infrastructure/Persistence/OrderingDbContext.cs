using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Checkout;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence;

/// <summary>Owns Order aggregates, checkout Sagas, HTTP idempotency, Inbox, and Outbox state.</summary>
public sealed class OrderingDbContext : DbContext
{
    /// <summary>Initializes the Ordering context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
    {
    }

    /// <summary>Gets Order aggregates.</summary>
    public DbSet<Order> Orders => Set<Order>();
    /// <summary>Gets historical order items.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    /// <summary>Gets explicit checkout process managers.</summary>
    public DbSet<CheckoutSagaState> CheckoutSagas => Set<CheckoutSagaState>();
    /// <summary>Gets durable HTTP checkout key mappings.</summary>
    public DbSet<CheckoutIdempotencyRecord> CheckoutIdempotency => Set<CheckoutIdempotencyRecord>();
    /// <summary>Gets pending integration events.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    /// <summary>Gets handled integration messages.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Configures aggregate snapshots and reliable-messaging tables.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");
        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(order => order.Id);
            builder.Property(order => order.Id).HasConversion(id => id.Value, value => new OrderId(value));
            builder.HasIndex(order => order.OrderNumber).IsUnique();
            builder.Property(order => order.OrderNumber).HasMaxLength(40);
            builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(order => order.CancellationReason).HasMaxLength(500);
            builder.Ignore(order => order.TotalAmount);
            builder.Ignore(order => order.Currency);
            builder.Ignore(order => order.DomainEvents);
            builder.OwnsOne(order => order.ShippingAddress, address =>
            {
                address.Property(value => value.RecipientName).HasColumnName("recipient_name").HasMaxLength(200);
                address.Property(value => value.Line1).HasColumnName("address_line_1").HasMaxLength(300);
                address.Property(value => value.City).HasColumnName("city").HasMaxLength(100);
                address.Property(value => value.PostalCode).HasColumnName("postal_code").HasMaxLength(30);
                address.Property(value => value.CountryCode).HasColumnName("country_code").HasMaxLength(2);
            });
            builder.HasMany(order => order.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("order_items");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasConversion(id => id.Value, value => new OrderItemId(value));
            builder.Property(item => item.Sku).HasMaxLength(64);
            builder.Property(item => item.ProductName).HasMaxLength(200);
            builder.Ignore(item => item.LineTotal);
            builder.OwnsOne(item => item.UnitPrice, price =>
            {
                price.Property(value => value.Amount).HasColumnName("unit_price").HasPrecision(18, 2);
                price.Property(value => value.Currency).HasColumnName("currency").HasMaxLength(3);
            });
        });
        modelBuilder.Entity<CheckoutSagaState>(builder =>
        {
            builder.ToTable("checkout_sagas");
            builder.HasKey(saga => saga.OrderId);
            builder.Property(saga => saga.Status).HasConversion<string>().HasMaxLength(40);
            builder.HasIndex(saga => saga.CorrelationId).IsUnique();
        });
        modelBuilder.Entity<CheckoutIdempotencyRecord>(builder =>
        {
            builder.ToTable("checkout_idempotency");
            builder.HasKey(record => new { record.CustomerId, record.Key });
            builder.Property(record => record.Key).HasMaxLength(200);
            builder.HasIndex(record => record.OrderId).IsUnique();
        });
        modelBuilder.ConfigureCommerceMessaging();
    }
}
