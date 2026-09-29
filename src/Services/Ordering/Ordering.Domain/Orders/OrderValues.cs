using Commerce.BuildingBlocks.Domain.Results;

namespace Ordering.Domain.Orders;

/// <summary>Identifies an order aggregate.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct OrderId(Guid Value)
{
    /// <summary>Creates a new order identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static OrderId New() => new(Guid.NewGuid());
}

/// <summary>Identifies one item inside an order.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct OrderItemId(Guid Value)
{
    /// <summary>Creates a new order-item identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static OrderItemId New() => new(Guid.NewGuid());
}

/// <summary>Represents an immutable monetary snapshot.</summary>
/// <param name="Amount">Non-negative monetary amount.</param>
/// <param name="Currency">Three-letter ISO-like currency code.</param>
public sealed record Money(decimal Amount, string Currency)
{
    /// <summary>Creates a validated monetary snapshot.</summary>
    /// <param name="amount">Non-negative amount.</param>
    /// <param name="currency">Three-letter currency code.</param>
    /// <returns>A validated money value or validation error.</returns>
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0 || string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return OrderErrors.InvalidMoney;
        }

        return new Money(decimal.Round(amount, 2), currency.Trim().ToUpperInvariant());
    }
}

/// <summary>Stores the historical delivery address captured at checkout.</summary>
/// <param name="RecipientName">Recipient full name.</param>
/// <param name="Line1">Primary address line.</param>
/// <param name="City">City or locality.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">Two-letter country code.</param>
public sealed record ShippingAddress(
    string RecipientName,
    string Line1,
    string City,
    string PostalCode,
    string CountryCode)
{
    /// <summary>Creates a validated shipping address.</summary>
    /// <param name="recipientName">Recipient full name.</param>
    /// <param name="line1">Primary address line.</param>
    /// <param name="city">City or locality.</param>
    /// <param name="postalCode">Postal code.</param>
    /// <param name="countryCode">Two-letter country code.</param>
    /// <returns>A validated address or validation error.</returns>
    public static Result<ShippingAddress> Create(
        string recipientName,
        string line1,
        string city,
        string postalCode,
        string countryCode)
    {
        if (string.IsNullOrWhiteSpace(recipientName) || string.IsNullOrWhiteSpace(line1) ||
            string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(postalCode) ||
            string.IsNullOrWhiteSpace(countryCode) || countryCode.Trim().Length != 2)
        {
            return OrderErrors.InvalidShippingAddress;
        }

        return new ShippingAddress(
            recipientName.Trim(), line1.Trim(), city.Trim(), postalCode.Trim(), countryCode.Trim().ToUpperInvariant());
    }
}

/// <summary>Defines externally meaningful order workflow states.</summary>
public enum OrderStatus
{
    /// <summary>The order has been created locally.</summary>
    Pending,
    /// <summary>The saga is awaiting Inventory.</summary>
    AwaitingInventory,
    /// <summary>Inventory accepted all reservation requests.</summary>
    InventoryReserved,
    /// <summary>The saga is awaiting Payment.</summary>
    AwaitingPayment,
    /// <summary>Payment succeeded.</summary>
    Paid,
    /// <summary>Fulfilment is preparing the order.</summary>
    Processing,
    /// <summary>Shipping has dispatched the order.</summary>
    Shipped,
    /// <summary>Shipping confirmed delivery.</summary>
    Delivered,
    /// <summary>The saga terminated and compensations were requested.</summary>
    Cancelled,
    /// <summary>A previously paid order was refunded.</summary>
    Refunded
}

/// <summary>Defines Order aggregate failures.</summary>
public static class OrderErrors
{
    /// <summary>Returned when checkout contains no items.</summary>
    public static readonly Error EmptyOrder = Error.Validation("Ordering.EmptyOrder", "An order must contain at least one item.");
    /// <summary>Returned when a monetary snapshot is invalid.</summary>
    public static readonly Error InvalidMoney = Error.Validation("Ordering.InvalidMoney", "Money requires a non-negative amount and three-letter currency.");
    /// <summary>Returned when the shipping address is incomplete.</summary>
    public static readonly Error InvalidShippingAddress = Error.Validation("Ordering.InvalidAddress", "A complete shipping address is required.");
    /// <summary>Returned when an order item is invalid.</summary>
    public static readonly Error InvalidItem = Error.Validation("Ordering.InvalidItem", "Order items require product data, a positive quantity and a valid price.");
    /// <summary>Returned when a workflow transition is not valid from the current state.</summary>
    public static readonly Error InvalidTransition = Error.Conflict("Ordering.InvalidTransition", "The requested order state transition is not valid.");
}
