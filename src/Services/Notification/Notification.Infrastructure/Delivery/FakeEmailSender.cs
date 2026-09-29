using Microsoft.Extensions.Logging;
using Notification.Application.Notifications;

namespace Notification.Infrastructure.Delivery;

/// <summary>Writes development email deliveries to structured logs without SMTP credentials.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ILogger<FakeEmailSender> _logger;

    /// <summary>Initializes the fake email sender.</summary>
    /// <param name="logger">Structured logger.</param>
    public FakeEmailSender(ILogger<FakeEmailSender> logger) => _logger = logger;

    /// <inheritdoc />
    public Task SendAsync(
        Guid messageId,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Fake email {MessageId} to {Recipient}: {Subject} - {Body}",
            messageId,
            recipient,
            subject,
            body);
        return Task.CompletedTask;
    }
}
