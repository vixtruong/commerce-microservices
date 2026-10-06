using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Inventory.Domain.Stock;

/// <summary>
/// Owns stock counts and reservations for one Catalog product without referencing Catalog domain types.
/// </summary>
public sealed class StockItem : AggregateRoot<StockItemId>
{
    private readonly List<StockReservation> _reservations = [];

    private StockItem()
    {
    }

    private StockItem(StockItemId id, Guid productId, DateTimeOffset createdAtUtc)
        : base(id)
    {
        ProductId = productId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the Catalog product identifier represented by this stock item.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Gets all physically owned units, including currently reserved units.</summary>
    public int QuantityOnHand { get; private set; }

    /// <summary>Gets units held by pending reservations.</summary>
    public int ReservedQuantity { get; private set; }

    /// <summary>Gets units that can still be reserved.</summary>
    public int AvailableQuantity => QuantityOnHand - ReservedQuantity;

    /// <summary>Adjusts physical units without removing units held by checkout reservations.</summary>
    /// <param name="delta">Signed nonzero unit change.</param>
    /// <param name="changedAtUtc">UTC change time.</param>
    /// <returns>Success or a stock-invariant error.</returns>
    public Result AdjustStock(int delta, DateTimeOffset changedAtUtc)
    {
        long target = (long)QuantityOnHand + delta;
        // Leased units belong to in-flight checkout; an administrator cannot erase those holds.
        if (delta == 0 || target < ReservedQuantity || target > int.MaxValue)
            return Error.Conflict("Inventory.InvalidAdjustment", "Adjustment must preserve reserved units and fit the stock range.");
        QuantityOnHand = (int)target;
        Touch(changedAtUtc);
        return Result.Success();
    }

    /// <summary>
    /// Gets the optimistic concurrency token incremented by every aggregate mutation.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Gets the UTC time of the most recent mutation.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Gets reservations owned by this aggregate.</summary>
    public IReadOnlyCollection<StockReservation> Reservations => _reservations.AsReadOnly();

    /// <summary>Creates an empty stock item for a product.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <param name="stockItemId">Optional stable identifier used by deterministic imports and seed data.</param>
    /// <returns>A new stock item.</returns>
    /// <exception cref="ArgumentException">Thrown when the product identifier is empty.</exception>
    public static StockItem Create(
        Guid productId,
        DateTimeOffset createdAtUtc,
        StockItemId? stockItemId = null)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id must not be empty.", nameof(productId));
        }

        return new StockItem(stockItemId ?? StockItemId.New(), productId, createdAtUtc);
    }

    /// <summary>Increases physically owned stock.</summary>
    /// <param name="quantity">Positive number of units received.</param>
    /// <param name="changedAtUtc">UTC mutation time.</param>
    /// <returns>A success, validation, or overflow result.</returns>
    public Result IncreaseStock(int quantity, DateTimeOffset changedAtUtc)
    {
        if (quantity <= 0)
        {
            return InventoryErrors.QuantityMustBePositive;
        }

        try
        {
            // A checked addition deliberately converts integer overflow into a domain failure.
            QuantityOnHand = checked(QuantityOnHand + quantity);
        }
        catch (OverflowException)
        {
            return InventoryErrors.QuantityOverflow;
        }

        Touch(changedAtUtc);
        return Result.Success();
    }

    /// <summary>Creates or replays an idempotent order reservation.</summary>
    /// <param name="orderId">Ordering aggregate identifier.</param>
    /// <param name="correlationId">Checkout correlation identifier.</param>
    /// <param name="quantity">Positive number of units to hold.</param>
    /// <param name="expiresAtUtc">UTC lease expiry time.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <returns>The existing or newly-created reservation identifier.</returns>
    public Result<StockReservationId> Reserve(
        Guid orderId,
        Guid correlationId,
        int quantity,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            return InventoryErrors.OrderIdRequired;
        }

        if (correlationId == Guid.Empty)
        {
            return InventoryErrors.CorrelationIdRequired;
        }

        if (quantity <= 0)
        {
            return InventoryErrors.QuantityMustBePositive;
        }

        StockReservation? replay = _reservations.SingleOrDefault(reservation => reservation.OrderId == orderId);
        if (replay is not null)
        {
            // The order/product pair is the idempotency boundary for reservation requests.
            return replay.Id;
        }

        if (AvailableQuantity < quantity)
        {
            return InventoryErrors.InsufficientStock;
        }

        var reservation = new StockReservation(
            StockReservationId.New(), Id, ProductId, orderId, correlationId, quantity, createdAtUtc, expiresAtUtc);
        _reservations.Add(reservation);
        ReservedQuantity = checked(ReservedQuantity + quantity);
        Touch(createdAtUtc);
        return reservation.Id;
    }

    /// <summary>Confirms a paid reservation and deducts its units from on-hand stock.</summary>
    /// <param name="reservationId">Reservation to confirm.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or domain failure.</returns>
    public Result ConfirmReservation(StockReservationId reservationId, DateTimeOffset completedAtUtc)
    {
        StockReservation? reservation = FindReservation(reservationId);
        if (reservation is null)
        {
            return InventoryErrors.ReservationNotFound;
        }

        Result result = reservation.Confirm(completedAtUtc);
        if (result.IsFailure)
        {
            return result;
        }

        ReservedQuantity -= reservation.Quantity;
        QuantityOnHand -= reservation.Quantity;
        Touch(completedAtUtc);
        return Result.Success();
    }

    /// <summary>Releases a pending reservation after checkout compensation.</summary>
    /// <param name="reservationId">Reservation to release.</param>
    /// <param name="reason">Compensation reason.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or domain failure.</returns>
    public Result ReleaseReservation(
        StockReservationId reservationId,
        string reason,
        DateTimeOffset completedAtUtc)
    {
        StockReservation? reservation = FindReservation(reservationId);
        if (reservation is null)
        {
            return InventoryErrors.ReservationNotFound;
        }

        Result result = reservation.Release(reason, completedAtUtc);
        if (result.IsFailure)
        {
            return result;
        }

        ReservedQuantity -= reservation.Quantity;
        Touch(completedAtUtc);
        return Result.Success();
    }

    /// <summary>Expires a pending reservation after its lease elapses.</summary>
    /// <param name="reservationId">Reservation to expire.</param>
    /// <param name="completedAtUtc">UTC expiry processing time.</param>
    /// <returns>A success or domain failure.</returns>
    public Result ExpireReservation(StockReservationId reservationId, DateTimeOffset completedAtUtc)
    {
        StockReservation? reservation = FindReservation(reservationId);
        if (reservation is null)
        {
            return InventoryErrors.ReservationNotFound;
        }

        Result result = reservation.Expire(completedAtUtc);
        if (result.IsFailure)
        {
            return result;
        }

        // Expiry releases only the hold; physical stock remains available for another checkout.
        ReservedQuantity -= reservation.Quantity;
        Touch(completedAtUtc);
        return Result.Success();
    }

    /// <summary>Locates a child reservation by strongly typed identifier.</summary>
    /// <param name="reservationId">Reservation identifier.</param>
    /// <returns>The matching reservation, or null.</returns>
    private StockReservation? FindReservation(StockReservationId reservationId) =>
        _reservations.SingleOrDefault(reservation => reservation.Id == reservationId);

    /// <summary>Advances the optimistic version after a state mutation.</summary>
    /// <param name="changedAtUtc">UTC mutation time.</param>
    private void Touch(DateTimeOffset changedAtUtc)
    {
        UpdatedAtUtc = changedAtUtc;
        Version = checked(Version + 1);
    }
}
