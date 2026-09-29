using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Shipping.Domain.Shipments;

/// <summary>Identifies a shipment aggregate.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct ShipmentId(Guid Value)
{
    /// <summary>Creates a new shipment identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static ShipmentId New() => new(Guid.NewGuid());
}

/// <summary>Defines shipment fulfilment states.</summary>
public enum ShipmentStatus
{
    /// <summary>The shipment record exists.</summary>
    Created,
    /// <summary>The package is ready for carrier pickup.</summary>
    ReadyForPickup,
    /// <summary>The carrier is transporting the package.</summary>
    InTransit,
    /// <summary>The package was delivered.</summary>
    Delivered,
    /// <summary>Fulfilment was cancelled.</summary>
    Cancelled
}

/// <summary>Represents the delivery address owned by Shipping.</summary>
/// <param name="RecipientName">Recipient name.</param>
/// <param name="Line1">Primary address line.</param>
/// <param name="City">City.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">Country code.</param>
public sealed record DeliveryAddress(
    string RecipientName,
    string Line1,
    string City,
    string PostalCode,
    string CountryCode);

/// <summary>Defines Shipment aggregate failures.</summary>
public static class ShipmentErrors
{
    /// <summary>Returned for a missing order or incomplete address.</summary>
    public static readonly Error InvalidShipment = Error.Validation("Shipping.Invalid", "Shipment requires an order and delivery address.");
    /// <summary>Returned for an invalid fulfilment transition.</summary>
    public static readonly Error InvalidTransition = Error.Conflict("Shipping.InvalidTransition", "The shipment state transition is invalid.");
}

/// <summary>Owns one order's fulfilment lifecycle and tracking number.</summary>
public sealed class Shipment : AggregateRoot<ShipmentId>
{
    private Shipment()
    {
    }

    private Shipment(
        ShipmentId id,
        Guid orderId,
        Guid correlationId,
        DeliveryAddress deliveryAddress,
        string trackingNumber,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        OrderId = orderId;
        CorrelationId = correlationId;
        DeliveryAddress = deliveryAddress;
        TrackingNumber = trackingNumber;
        Status = ShipmentStatus.Created;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the unique source order identifier.</summary>
    public Guid OrderId { get; private set; }
    /// <summary>Gets the checkout workflow correlation identifier.</summary>
    public Guid CorrelationId { get; private set; }
    /// <summary>Gets the Shipping-owned address snapshot.</summary>
    public DeliveryAddress DeliveryAddress { get; private set; } = null!;
    /// <summary>Gets the development or carrier tracking number.</summary>
    public string TrackingNumber { get; private set; } = string.Empty;
    /// <summary>Gets the shipment state.</summary>
    public ShipmentStatus Status { get; private set; }
    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC most recent transition time.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates one idempotency-keyed shipment for an order.</summary>
    /// <param name="orderId">Paid order identifier.</param>
    /// <param name="correlationId">Checkout workflow correlation identifier.</param>
    /// <param name="address">Delivery address snapshot.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <returns>A shipment or validation error.</returns>
    public static Result<Shipment> Create(
        Guid orderId,
        Guid correlationId,
        DeliveryAddress address,
        DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty || string.IsNullOrWhiteSpace(address.RecipientName) ||
            string.IsNullOrWhiteSpace(address.Line1))
        {
            return ShipmentErrors.InvalidShipment;
        }

        return new Shipment(
            ShipmentId.New(), orderId, correlationId, address, $"DEV-{Guid.NewGuid():N}"[..20].ToUpperInvariant(), createdAtUtc);
    }

    /// <summary>Moves a created shipment to pickup readiness.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkReadyForPickup(DateTimeOffset changedAtUtc) =>
        Transition(ShipmentStatus.Created, ShipmentStatus.ReadyForPickup, changedAtUtc);

    /// <summary>Records carrier pickup.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkInTransit(DateTimeOffset changedAtUtc) =>
        Transition(ShipmentStatus.ReadyForPickup, ShipmentStatus.InTransit, changedAtUtc);

    /// <summary>Records successful delivery.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkDelivered(DateTimeOffset changedAtUtc) =>
        Transition(ShipmentStatus.InTransit, ShipmentStatus.Delivered, changedAtUtc);

    /// <summary>Applies an exact shipment transition.</summary>
    /// <param name="expected">Required current state.</param>
    /// <param name="next">Target state.</param>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    private Result Transition(ShipmentStatus expected, ShipmentStatus next, DateTimeOffset changedAtUtc)
    {
        if (Status != expected)
        {
            return ShipmentErrors.InvalidTransition;
        }

        Status = next;
        UpdatedAtUtc = changedAtUtc;
        return Result.Success();
    }
}
