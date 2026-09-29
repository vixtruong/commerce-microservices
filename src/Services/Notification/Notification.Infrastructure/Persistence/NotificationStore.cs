using Microsoft.EntityFrameworkCore;
using Notification.Application.Notifications;

namespace Notification.Infrastructure.Persistence;

/// <summary>Provides durable idempotent notification ingestion.</summary>
public sealed class NotificationStore : INotificationStore
{
    private readonly NotificationDbContext _dbContext;

    /// <summary>Initializes the notification store.</summary>
    /// <param name="dbContext">Notification context.</param>
    public NotificationStore(NotificationDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task AddIfNewAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (await _dbContext.Notifications.AsNoTracking().AnyAsync(item => item.Id == message.Id, cancellationToken)) return;
        _dbContext.Notifications.Add(message);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!cancellationToken.IsCancellationRequested)
        {
            // A concurrent redelivery can lose the insert race; the primary key proves it is already durable.
        }
    }
}
