using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Payment.Domain.Payments;

namespace Payment.Infrastructure.Persistence;

/// <summary>Owns Payment aggregates and reliable message state.</summary>
public sealed class PaymentDbContext : DbContext
{
    /// <summary>Initializes the Payment context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    /// <summary>Gets payments.</summary>
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    /// <summary>Gets pending integration events.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    /// <summary>Gets handled integration messages.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Configures payment idempotency and messaging tables.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payment");
        modelBuilder.Entity<PaymentRecord>(builder =>
        {
            builder.ToTable("payments");
            builder.HasKey(payment => payment.Id);
            builder.Property(payment => payment.Id).HasConversion(id => id.Value, value => new PaymentId(value));
            builder.HasIndex(payment => payment.OrderId).IsUnique();
            builder.Property(payment => payment.Amount).HasPrecision(18, 2);
            builder.Property(payment => payment.Currency).HasMaxLength(3);
            builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(30);
            builder.Property(payment => payment.TransactionReference).HasMaxLength(200);
            builder.Property(payment => payment.FailureCode).HasMaxLength(100);
            builder.Ignore(payment => payment.DomainEvents);
        });
        modelBuilder.ConfigureCommerceMessaging();
    }
}
