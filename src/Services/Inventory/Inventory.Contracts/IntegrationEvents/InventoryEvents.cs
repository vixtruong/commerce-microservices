using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Inventory.Contracts.IntegrationEvents;

/// <summary>Defines versioned routing keys published by Inventory.</summary>
public static class InventoryEventNames
{
    /// <summary>Routing key for successful reservations.</summary>
    public const string InventoryReservedV1 = "inventory.reserved.v1";
    /// <summary>Routing key for rejected reservations.</summary>
    public const string InventoryReservationFailedV1 = "inventory.reservation-failed.v1";
    /// <summary>Routing key for completed compensation.</summary>
    public const string InventoryReleasedV1 = "inventory.released.v1";
    /// <summary>Routing key for reservation lease expiry.</summary>
    public const string InventoryReservationExpiredV1 = "inventory.reservation-expired.v1";
}

/// <summary>Identifies one Inventory reservation created for an order.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="ReservationId">Reservation identifier.</param>
public sealed record InventoryReservationReferenceV1(Guid ProductId, Guid ReservationId);

/// <summary>Announces that every requested product was reserved.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Reservation request identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Reservations">Created reservation identifiers.</param>
public sealed record InventoryReservedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    IReadOnlyCollection<InventoryReservationReferenceV1> Reservations) : IIntegrationEvent;

/// <summary>Announces that inventory could not satisfy an order.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Reservation request identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Reason">Stable failure reason.</param>
public sealed record InventoryReservationFailedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    string Reason) : IIntegrationEvent;

/// <summary>Announces completion of reservation compensation.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Release request identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
public sealed record InventoryReleasedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId) : IIntegrationEvent;

/// <summary>Announces that Inventory released an order's holds after their lease elapsed.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Optional triggering message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order whose reservation expired.</param>
public sealed record InventoryReservationExpiredIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId) : IIntegrationEvent;
