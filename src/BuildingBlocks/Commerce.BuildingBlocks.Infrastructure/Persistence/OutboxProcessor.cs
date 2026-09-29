using Commerce.BuildingBlocks.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Reliably publishes locally committed outbox rows with bounded retries and expiring claims.
/// </summary>
/// <typeparam name="TDbContext">Service-owned context containing an OutboxMessages set.</typeparam>
public sealed class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly Guid _workerId = Guid.NewGuid();

    /// <summary>Initializes an outbox processor.</summary>
    /// <param name="scopeFactory">Factory used to create short-lived database scopes.</param>
    /// <param name="eventBus">Broker publisher.</param>
    /// <param name="logger">Structured logger.</param>
    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IEventBus eventBus,
        ILogger<OutboxProcessor<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>Runs the polling loop until shutdown.</summary>
    /// <param name="stoppingToken">Application shutdown token.</param>
    /// <returns>A task representing the processor lifetime.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed = await ProcessBatchAsync(stoppingToken);
            await Task.Delay(processed == 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }

    /// <summary>Claims and publishes one bounded batch.</summary>
    /// <param name="cancellationToken">Token used to cancel database and broker work.</param>
    /// <returns>The number of claimed rows.</returns>
    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        TDbContext dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        DbSet<OutboxMessage> outbox = dbContext.Set<OutboxMessage>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Guid[] candidates = await outbox
            .AsNoTracking()
            .Where(message => message.ProcessedOnUtc == null && message.RetryCount < 10 &&
                (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
            .OrderBy(message => message.OccurredOnUtc)
            .Select(message => message.Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        foreach (Guid candidate in candidates)
        {
            int claimed = await outbox
                .Where(message => message.Id == candidate && message.ProcessedOnUtc == null &&
                    (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.LockId, _workerId)
                    .SetProperty(message => message.LockedUntilUtc, now.AddSeconds(30)), cancellationToken);

            if (claimed == 0)
            {
                continue;
            }

            OutboxMessage message = await outbox.SingleAsync(
                item => item.Id == candidate && item.LockId == _workerId,
                cancellationToken);

            try
            {
                await _eventBus.PublishAsync(message.ToEnvelope(), cancellationToken);
                message.ProcessedOnUtc = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception exception)
            {
                message.RetryCount++;
                message.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                _logger.LogError(exception, "Outbox publication failed for message {MessageId}", message.Id);
            }
            finally
            {
                message.LockId = null;
                message.LockedUntilUtc = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return candidates.Length;
    }
}
