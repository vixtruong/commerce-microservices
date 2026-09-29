using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Stores outbox and inbox rows in a service-owned EF Core context.</summary>
/// <typeparam name="TDbContext">Service database context.</typeparam>
public sealed class EfTransactionalMessaging<TDbContext> : IOutbox, IInbox
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    /// <summary>Initializes transactional messaging storage.</summary>
    /// <param name="dbContext">Scoped service context.</param>
    public EfTransactionalMessaging(TDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public void Add<TEvent>(TEvent integrationEvent, string type)
        where TEvent : IIntegrationEvent =>
        _dbContext.Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent, type));

    /// <inheritdoc />
    public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) =>
        _dbContext.Set<InboxMessage>().AsNoTracking().AnyAsync(
            message => message.MessageId == messageId && message.Consumer == consumer,
            cancellationToken);

    /// <inheritdoc />
    public void MarkProcessed(Guid messageId, string consumer) =>
        _dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = messageId,
            Consumer = consumer,
            ProcessedOnUtc = DateTimeOffset.UtcNow
        });
}
