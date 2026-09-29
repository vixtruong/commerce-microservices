using Microsoft.EntityFrameworkCore;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Configures the shared technical outbox and inbox tables inside each service-owned schema.
/// </summary>
public static class MessagingModelBuilderExtensions
{
    /// <summary>Adds indexes and bounded columns required by reliable messaging.</summary>
    /// <param name="modelBuilder">Service model builder.</param>
    public static void ConfigureCommerceMessaging(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(300).IsRequired();
            builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(message => message.Error).HasMaxLength(2000);
            builder.HasIndex(message => new { message.ProcessedOnUtc, message.OccurredOnUtc });
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(message => new { message.MessageId, message.Consumer });
            builder.Property(message => message.Consumer).HasMaxLength(200);
        });
    }
}
