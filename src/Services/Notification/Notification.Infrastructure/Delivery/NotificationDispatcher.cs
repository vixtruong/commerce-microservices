using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Application.Notifications;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Delivery;

/// <summary>Dispatches durable notification work with retry while supporting graceful shutdown.</summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>Initializes the dispatcher.</summary>
    /// <param name="scopeFactory">Factory for short-lived persistence scopes.</param>
    /// <param name="logger">Structured logger.</param>
    public NotificationDispatcher(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Polls unsent work until shutdown.</summary>
    /// <param name="stoppingToken">Shutdown token.</param>
    /// <returns>A task representing the worker lifetime.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            NotificationDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            IEmailSender sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            NotificationMessage[] messages = await dbContext.Notifications
                .Where(message => message.SentAtUtc == null && message.RetryCount < 10)
                .OrderBy(message => message.CreatedAtUtc)
                .Take(20)
                .ToArrayAsync(stoppingToken);
            foreach (NotificationMessage message in messages)
            {
                try
                {
                    await sender.SendAsync(message.Id, message.Recipient, message.Subject, message.Body, stoppingToken);
                    message.MarkSent(DateTimeOffset.UtcNow);
                }
                catch (Exception exception)
                {
                    message.RecordFailure(exception.Message);
                    _logger.LogError(exception, "Notification dispatch failed for {MessageId}", message.Id);
                }
            }

            if (messages.Length > 0) await dbContext.SaveChangesAsync(stoppingToken);
            await Task.Delay(messages.Length == 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }
}
