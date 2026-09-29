using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Payment.Contracts.IntegrationEvents;

/// <summary>Defines versioned routing keys published by Payment.</summary>
public static class PaymentEventNames
{
    /// <summary>Routing key for an approved payment.</summary>
    public const string PaymentSucceededV1 = "payment.succeeded.v1";
    /// <summary>Routing key for a failed payment.</summary>
    public const string PaymentFailedV1 = "payment.failed.v1";
}

/// <summary>Announces that a payment provider approved an order.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Payment request identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="PaymentId">Payment identifier.</param>
/// <param name="TransactionReference">Provider transaction reference.</param>
public sealed record PaymentSucceededIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    Guid PaymentId,
    string TransactionReference) : IIntegrationEvent;

/// <summary>Announces that payment could not complete.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Payment request identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="PaymentId">Payment identifier.</param>
/// <param name="Reason">Sanitized failure code.</param>
public sealed record PaymentFailedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    Guid PaymentId,
    string Reason) : IIntegrationEvent;
