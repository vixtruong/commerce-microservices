using Microsoft.EntityFrameworkCore;
using Notification.Application.Notifications;

namespace Notification.Infrastructure.Persistence;

/// <summary>Owns durable, source-message-keyed notification work.</summary>
public sealed class NotificationDbContext : DbContext
{
    /// <summary>Initializes the Notification context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    /// <summary>Gets durable notification work items.</summary>
    public DbSet<NotificationMessage> Notifications => Set<NotificationMessage>();

    /// <summary>Configures source message idempotency and retry fields.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");
        modelBuilder.Entity<NotificationMessage>(builder =>
        {
            builder.ToTable("notifications");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Recipient).HasMaxLength(200);
            builder.Property(message => message.Subject).HasMaxLength(300);
            builder.Property(message => message.Body).HasMaxLength(2000);
            builder.Property(message => message.Error).HasMaxLength(1000);
            builder.HasIndex(message => new { message.SentAtUtc, message.CreatedAtUtc });
        });
    }
}
