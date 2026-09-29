using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Ordering.Contracts.IntegrationEvents;

/// <summary>Defines versioned routing keys published by Ordering.</summary>
public static class OrderingEventNames
{
    /// <summary>Routing key for a newly created order.</summary>
    public const string OrderCreatedV1 = "ordering.order-created.v1";
    /// <summary>Routing key requesting inventory reservations.</summary>
    public const string InventoryReservationRequestedV1 = "ordering.inventory-reservation-requested.v1";
    /// <summary>Routing key requesting reservation compensation.</summary>
    public const string InventoryReleaseRequestedV1 = "ordering.inventory-release-requested.v1";
    /// <summary>Routing key requesting payment processing.</summary>
    public const string PaymentRequestedV1 = "ordering.payment-requested.v1";
    /// <summary>Routing key announcing a paid order.</summary>
    public const string OrderPaidV1 = "ordering.order-paid.v1";
    /// <summary>Routing key announcing order cancellation.</summary>
    public const string OrderCancelledV1 = "ordering.order-cancelled.v1";
}

/// <summary>Provides one product quantity requested from Inventory.</summary>
/// <param name="ProductId">Catalog product identifier.</param>
/// <param name="Quantity">Positive reservation quantity.</param>
public sealed record InventoryReservationLineV1(Guid ProductId, int Quantity);

/// <summary>Requests idempotent inventory holds for a newly created order.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Causing message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="ExpiresAtUtc">Reservation lease expiry.</param>
/// <param name="Items">Product quantities to reserve.</param>
public sealed record InventoryReservationRequestedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<InventoryReservationLineV1> Items) : IIntegrationEvent;

/// <summary>Requests payment for an inventory-secured order.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Inventory outcome message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Amount">Order total.</param>
/// <param name="Currency">Three-letter currency.</param>
public sealed record PaymentRequestedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    decimal Amount,
    string Currency) : IIntegrationEvent;

/// <summary>Requests compensating release after payment failure.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Payment failure message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Reason">Cancellation reason.</param>
public sealed record InventoryReleaseRequestedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    string Reason) : IIntegrationEvent;

/// <summary>Announces order creation for audit and notifications.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Causing command identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="OrderNumber">Human-readable order number.</param>
public sealed record OrderCreatedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber) : IIntegrationEvent;

/// <summary>Announces successful payment and provides the Shipping address snapshot.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Payment outcome message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="RecipientName">Recipient name.</param>
/// <param name="AddressLine1">Primary address line.</param>
/// <param name="City">City.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">Country code.</param>
public sealed record OrderPaidIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    string RecipientName,
    string AddressLine1,
    string City,
    string PostalCode,
    string CountryCode) : IIntegrationEvent;

/// <summary>Announces terminal order cancellation.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Failure message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Reason">Cancellation reason.</param>
public sealed record OrderCancelledIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    string Reason) : IIntegrationEvent;
