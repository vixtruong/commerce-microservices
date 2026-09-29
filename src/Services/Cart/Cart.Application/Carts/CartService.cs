using Commerce.BuildingBlocks.Domain.Results;

namespace Cart.Application.Carts;

/// <summary>Represents a product snapshot stored in a customer's Redis cart.</summary>
/// <param name="ProductId">Catalog product identifier.</param>
/// <param name="Sku">SKU snapshot.</param>
/// <param name="ProductName">Name snapshot.</param>
/// <param name="UnitPrice">Price snapshot for display; Ordering revalidates it.</param>
/// <param name="Currency">Currency snapshot.</param>
/// <param name="Quantity">Positive quantity.</param>
public sealed record CartItem(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity);

/// <summary>Represents one customer cart persisted in Redis.</summary>
/// <param name="CustomerId">Authenticated customer identifier.</param>
/// <param name="Items">Current cart items.</param>
/// <param name="UpdatedAtUtc">UTC most recent mutation time.</param>
public sealed record CustomerCart(Guid CustomerId, IReadOnlyCollection<CartItem> Items, DateTimeOffset UpdatedAtUtc);

/// <summary>Represents an authoritative Catalog snapshot used while editing a cart.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Sku">SKU.</param>
/// <param name="Name">Display name.</param>
/// <param name="Price">Current price.</param>
/// <param name="Currency">Currency.</param>
/// <param name="IsActive">Whether the product may be purchased.</param>
public sealed record CatalogProductSnapshot(
    Guid ProductId,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);

/// <summary>Defines Redis cart persistence.</summary>
public interface ICartStore
{
    /// <summary>Gets a cart or an empty cart for the customer.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>The current cart.</returns>
    Task<CustomerCart> GetAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Persists a cart and refreshes its expiration.</summary>
    /// <param name="cart">Cart to store.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>A task that completes after Redis accepts the value.</returns>
    Task SetAsync(CustomerCart cart, CancellationToken cancellationToken);

    /// <summary>Removes a cart.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>A task that completes after removal.</returns>
    Task DeleteAsync(Guid customerId, CancellationToken cancellationToken);
}

/// <summary>Defines bounded internal Catalog lookup operations.</summary>
public interface ICatalogProductClient
{
    /// <summary>Gets one authoritative product snapshot with a deadline and resilience policy.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The product, or null when not found.</returns>
    Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}

/// <summary>Defines cart validation failures.</summary>
public static class CartErrors
{
    /// <summary>Returned for non-positive quantities.</summary>
    public static readonly Error InvalidQuantity = Error.Validation("Cart.InvalidQuantity", "Cart quantity must be greater than zero.");
    /// <summary>Returned when Catalog cannot supply an active product.</summary>
    public static readonly Error ProductUnavailable = Error.Conflict("Cart.ProductUnavailable", "The product is not available for purchase.");
    /// <summary>Returned when an item is not present.</summary>
    public static readonly Error ItemNotFound = Error.NotFound("Cart.ItemNotFound", "The cart item was not found.");
}

/// <summary>Implements independent Redis cart use cases using Catalog contract snapshots only.</summary>
public sealed class CartService
{
    private readonly ICartStore _store;
    private readonly ICatalogProductClient _catalog;

    /// <summary>Initializes the cart service.</summary>
    /// <param name="store">Redis cart store.</param>
    /// <param name="catalog">Internal Catalog client.</param>
    public CartService(ICartStore store, ICatalogProductClient catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    /// <summary>Gets the authenticated customer's cart.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The cart.</returns>
    public Task<CustomerCart> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
        _store.GetAsync(customerId, cancellationToken);

    /// <summary>Adds or replaces a product using an authoritative Catalog snapshot.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="quantity">Positive quantity.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The updated cart or validation failure.</returns>
    public async Task<Result<CustomerCart>> SetItemAsync(
        Guid customerId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0) return CartErrors.InvalidQuantity;
        CatalogProductSnapshot? product = await _catalog.GetProductAsync(productId, cancellationToken);
        if (product is null || !product.IsActive) return CartErrors.ProductUnavailable;

        CustomerCart current = await _store.GetAsync(customerId, cancellationToken);
        List<CartItem> items = current.Items.Where(item => item.ProductId != productId).ToList();
        items.Add(new CartItem(product.ProductId, product.Sku, product.Name, product.Price, product.Currency, quantity));
        var updated = new CustomerCart(customerId, items, DateTimeOffset.UtcNow);
        await _store.SetAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Removes one product from the cart.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="productId">Product to remove.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The updated cart or not-found failure.</returns>
    public async Task<Result<CustomerCart>> RemoveItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken)
    {
        CustomerCart current = await _store.GetAsync(customerId, cancellationToken);
        if (current.Items.All(item => item.ProductId != productId)) return CartErrors.ItemNotFound;
        var updated = current with
        {
            Items = current.Items.Where(item => item.ProductId != productId).ToArray(),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await _store.SetAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Clears the authenticated customer's cart.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>A task that completes after deletion.</returns>
    public Task ClearAsync(Guid customerId, CancellationToken cancellationToken) =>
        _store.DeleteAsync(customerId, cancellationToken);
}
