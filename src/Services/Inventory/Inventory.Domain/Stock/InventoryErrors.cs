using Commerce.BuildingBlocks.Domain.Results;

namespace Inventory.Domain.Stock;

/// <summary>Defines failures enforced by the Inventory aggregate.</summary>
public static class InventoryErrors
{
    /// <summary>Returned when a stock mutation uses a non-positive quantity.</summary>
    public static readonly Error QuantityMustBePositive = Error.Validation(
        "Inventory.QuantityMustBePositive",
        "Inventory quantity must be greater than zero.");

    /// <summary>Returned when a stock increase would overflow the supported integer range.</summary>
    public static readonly Error QuantityOverflow = Error.Validation(
        "Inventory.QuantityOverflow",
        "The resulting stock quantity exceeds the supported range.");

    /// <summary>Returned when available stock cannot satisfy a reservation.</summary>
    public static readonly Error InsufficientStock = Error.Conflict(
        "Inventory.InsufficientStock",
        "The requested quantity is not available.");

    /// <summary>Returned when a reservation cannot be located.</summary>
    public static readonly Error ReservationNotFound = Error.NotFound(
        "Inventory.ReservationNotFound",
        "The stock reservation was not found.");

    /// <summary>Returned when an already-completed reservation is changed.</summary>
    public static readonly Error ReservationNotPending = Error.Conflict(
        "Inventory.ReservationNotPending",
        "Only pending reservations can be confirmed, released, or expired.");

    /// <summary>Returned when an order identifier is missing.</summary>
    public static readonly Error OrderIdRequired = Error.Validation(
        "Inventory.OrderIdRequired",
        "An order identifier is required.");

    /// <summary>Returned when checkout correlation metadata is missing.</summary>
    public static readonly Error CorrelationIdRequired = Error.Validation(
        "Inventory.CorrelationIdRequired",
        "A correlation identifier is required.");
}
