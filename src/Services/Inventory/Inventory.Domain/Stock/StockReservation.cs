using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Inventory.Domain.Stock;

/// <summary>Identifies one Inventory stock item.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct StockItemId(Guid Value)
{
    /// <summary>Creates a new stock item identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static StockItemId New() => new(Guid.NewGuid());
}

/// <summary>Identifies one stock reservation.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct StockReservationId(Guid Value)
{
    /// <summary>Creates a new stock reservation identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static StockReservationId New() => new(Guid.NewGuid());
}

/// <summary>Describes the lifecycle of reserved units.</summary>
public enum StockReservationStatus
{
    /// <summary>The units are held while checkout continues.</summary>
    Pending,
    /// <summary>The order paid and the held units were deducted.</summary>
    Confirmed,
    /// <summary>A compensating action returned the units to availability.</summary>
    Released,
    /// <summary>The reservation lease elapsed before completion.</summary>
    Expired
}

/// <summary>
/// Represents an idempotent order-specific hold against one product's inventory.
/// </summary>
public sealed class StockReservation : Entity<StockReservationId>
{
    private StockReservation()
    {
    }

    internal StockReservation(
        StockReservationId id,
        StockItemId stockItemId,
        Guid productId,
        Guid orderId,
        Guid correlationId,
        int quantity,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        StockItemId = stockItemId;
        ProductId = productId;
        OrderId = orderId;
        CorrelationId = correlationId;
        Quantity = quantity;
        Status = StockReservationStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the owning stock item identifier.</summary>
    public StockItemId StockItemId { get; private set; }

    /// <summary>Gets the service-boundary product identifier.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Gets the order whose checkout owns this reservation.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the checkout correlation identifier propagated to expiry events.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Gets the held unit count.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the reservation lifecycle state.</summary>
    public StockReservationStatus Status { get; private set; }

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Gets the UTC lease expiry time.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Gets the UTC completion time when no longer pending.</summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Gets the completion or compensation reason.</summary>
    public string? CompletionReason { get; private set; }

    /// <summary>Confirms that payment completed and this hold may be deducted.</summary>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or invalid-state result.</returns>
    internal Result Confirm(DateTimeOffset completedAtUtc) =>
        Complete(StockReservationStatus.Confirmed, "payment-succeeded", completedAtUtc);

    /// <summary>Releases this hold as a compensating action.</summary>
    /// <param name="reason">Reason the hold is released.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or invalid-state result.</returns>
    internal Result Release(string reason, DateTimeOffset completedAtUtc) =>
        Complete(StockReservationStatus.Released, reason, completedAtUtc);

    /// <summary>Expires this hold after its bounded lease.</summary>
    /// <param name="completedAtUtc">UTC expiry processing time.</param>
    /// <returns>A success or invalid-state result.</returns>
    internal Result Expire(DateTimeOffset completedAtUtc) =>
        Complete(StockReservationStatus.Expired, "expired", completedAtUtc);

    /// <summary>Applies a single terminal transition.</summary>
    /// <param name="status">Target terminal status.</param>
    /// <param name="reason">Completion reason.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or invalid-state result.</returns>
    private Result Complete(StockReservationStatus status, string reason, DateTimeOffset completedAtUtc)
    {
        if (Status != StockReservationStatus.Pending)
        {
            return InventoryErrors.ReservationNotPending;
        }

        Status = status;
        CompletionReason = reason;
        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }
}
