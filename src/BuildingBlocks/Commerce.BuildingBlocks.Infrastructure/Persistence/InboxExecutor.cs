using Commerce.BuildingBlocks.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Executes a consumer mutation and inbox insert inside one local database transaction.
/// </summary>
public static class InboxExecutor
{
    /// <summary>Runs the mutation once for the given message and consumer.</summary>
    /// <typeparam name="TDbContext">Service database context.</typeparam>
    /// <typeparam name="TEvent">Integration event contract.</typeparam>
    /// <param name="dbContext">Context that owns both domain state and inbox.</param>
    /// <param name="integrationEvent">Broker-delivered event.</param>
    /// <param name="consumer">Stable handler identity.</param>
    /// <param name="mutation">Mutation that adds or updates tracked service state.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>A task that completes after the local transaction commits.</returns>
    public static async Task ExecuteOnceAsync<TDbContext, TEvent>(
        TDbContext dbContext,
        TEvent integrationEvent,
        string consumer,
        Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken)
        where TDbContext : DbContext
        where TEvent : IIntegrationEvent
    {
        bool processed = await dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .AnyAsync(message => message.MessageId == integrationEvent.MessageId && message.Consumer == consumer,
                cancellationToken);
        if (processed)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await mutation(cancellationToken);
        dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = integrationEvent.MessageId,
            Consumer = consumer,
            ProcessedOnUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
