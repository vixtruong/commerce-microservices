using Commerce.BuildingBlocks.Contracts.Messaging;
using Ordering.Contracts.IntegrationEvents;
using Payment.Contracts.IntegrationEvents;
using Shipping.Contracts.IntegrationEvents;

namespace Notification.Application.Notifications;

/// <summary>Represents one durable notification work item created from an integration event.</summary>
public sealed class NotificationMessage
{
    private NotificationMessage()
    {
    }

    /// <summary>Initializes a notification message.</summary>
    /// <param name="id">Source integration message identifier.</param>
    /// <param name="recipient">Non-sensitive development recipient.</param>
    /// <param name="subject">Notification subject.</param>
    /// <param name="body">Notification body.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    public NotificationMessage(Guid id, string recipient, string subject, string body, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the source message identifier and idempotency key.</summary>
    public Guid Id { get; private set; }
    /// <summary>Gets the fake recipient handle.</summary>
    public string Recipient { get; private set; } = string.Empty;
    /// <summary>Gets the notification subject.</summary>
    public string Subject { get; private set; } = string.Empty;
    /// <summary>Gets the notification body.</summary>
    public string Body { get; private set; } = string.Empty;
    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC dispatch time.</summary>
    public DateTimeOffset? SentAtUtc { get; private set; }
    /// <summary>Gets the last dispatch error.</summary>
    public string? Error { get; private set; }
    /// <summary>Gets the dispatch retry count.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Marks successful fake delivery.</summary>
    /// <param name="sentAtUtc">UTC dispatch time.</param>
    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        SentAtUtc = sentAtUtc;
        Error = null;
    }

    /// <summary>Records a bounded dispatch failure.</summary>
    /// <param name="error">Sanitized failure.</param>
    public void RecordFailure(string error)
    {
        RetryCount++;
        Error = error[..Math.Min(error.Length, 1000)];
    }
}

/// <summary>Persists idempotent notification work independently from RabbitMQ delivery.</summary>
public interface INotificationStore
{
    /// <summary>Adds a work item only when its source message id is new.</summary>
    /// <param name="message">Notification work item.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>A task that completes after the durable insert.</returns>
    Task AddIfNewAsync(NotificationMessage message, CancellationToken cancellationToken);
}

/// <summary>Converts supported workflow events into durable fake-notification work.</summary>
public sealed class NotificationHandler :
    IIntegrationEventHandler<OrderCreatedIntegrationEventV1>,
    IIntegrationEventHandler<OrderCancelledIntegrationEventV1>,
    IIntegrationEventHandler<PaymentSucceededIntegrationEventV1>,
    IIntegrationEventHandler<PaymentFailedIntegrationEventV1>,
    IIntegrationEventHandler<ShipmentCreatedIntegrationEventV1>,
    IIntegrationEventHandler<ShipmentDeliveredIntegrationEventV1>
{
    private readonly INotificationStore _store;

    /// <summary>Initializes the event-to-notification handler.</summary>
    /// <param name="store">Durable notification store.</param>
    public NotificationHandler(INotificationStore store) => _store = store;

    /// <inheritdoc />
    public Task HandleAsync(OrderCreatedIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"customer:{value.CustomerId:D}", "Order created", $"Order {value.OrderNumber} was created.", token);

    /// <inheritdoc />
    public Task HandleAsync(OrderCancelledIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"order:{value.OrderId:D}", "Order cancelled", $"Order was cancelled: {value.Reason}.", token);

    /// <inheritdoc />
    public Task HandleAsync(PaymentSucceededIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"order:{value.OrderId:D}", "Payment succeeded", "Payment completed successfully.", token);

    /// <inheritdoc />
    public Task HandleAsync(PaymentFailedIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"order:{value.OrderId:D}", "Payment failed", $"Payment failed: {value.Reason}.", token);

    /// <inheritdoc />
    public Task HandleAsync(ShipmentCreatedIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"order:{value.OrderId:D}", "Shipment created", $"Tracking number: {value.TrackingNumber}.", token);

    /// <inheritdoc />
    public Task HandleAsync(ShipmentDeliveredIntegrationEventV1 value, CancellationToken token) =>
        StoreAsync(value.MessageId, $"order:{value.OrderId:D}", "Shipment delivered", "The shipment was delivered.", token);

    /// <summary>Creates one source-message-keyed notification.</summary>
    /// <param name="messageId">Source integration message identifier.</param>
    /// <param name="recipient">Fake recipient handle.</param>
    /// <param name="subject">Subject.</param>
    /// <param name="body">Body.</param>
    /// <param name="cancellationToken">Token used to cancel persistence.</param>
    /// <returns>A task that completes after durable deduplication.</returns>
    private Task StoreAsync(
        Guid messageId,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken) =>
        _store.AddIfNewAsync(new NotificationMessage(messageId, recipient, subject, body, DateTimeOffset.UtcNow), cancellationToken);
}

/// <summary>Abstracts delivery of development email notifications.</summary>
public interface IEmailSender
{
    /// <summary>Sends one non-sensitive fake email.</summary>
    /// <param name="messageId">Stable notification idempotency key.</param>
    /// <param name="recipient">Fake recipient handle.</param>
    /// <param name="subject">Subject.</param>
    /// <param name="body">Body.</param>
    /// <param name="cancellationToken">Token used to cancel delivery.</param>
    /// <returns>A task that completes after delivery.</returns>
    Task SendAsync(Guid messageId, string recipient, string subject, string body, CancellationToken cancellationToken);
}
