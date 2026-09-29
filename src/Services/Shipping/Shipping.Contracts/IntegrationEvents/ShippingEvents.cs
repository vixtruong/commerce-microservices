using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Shipping.Contracts.IntegrationEvents;

/// <summary>Defines versioned routing keys published by Shipping.</summary>
public static class ShippingEventNames
{
    /// <summary>Routing key for shipment creation.</summary>
    public const string ShipmentCreatedV1 = "shipping.shipment-created.v1";
    /// <summary>Routing key for confirmed delivery.</summary>
    public const string ShipmentDeliveredV1 = "shipping.shipment-delivered.v1";
}

/// <summary>Announces creation of one paid order's shipment.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Paid-order message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="ShipmentId">Shipment identifier.</param>
/// <param name="TrackingNumber">Public tracking number.</param>
public sealed record ShipmentCreatedIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    Guid ShipmentId,
    string TrackingNumber) : IIntegrationEvent;

/// <summary>Announces final delivery.</summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Checkout correlation identifier.</param>
/// <param name="CausationId">Causing message identifier.</param>
/// <param name="OccurredOnUtc">UTC event time.</param>
/// <param name="OrderId">Order identifier.</param>
/// <param name="ShipmentId">Shipment identifier.</param>
public sealed record ShipmentDeliveredIntegrationEventV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredOnUtc,
    Guid OrderId,
    Guid ShipmentId) : IIntegrationEvent;
