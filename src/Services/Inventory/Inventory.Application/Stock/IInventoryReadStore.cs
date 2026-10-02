using Commerce.BuildingBlocks.Application.Queries;

namespace Inventory.Application.Stock;

/// <summary>Defines bounded administrative stock and reservation queries.</summary>
public interface IInventoryReadStore
{
    /// <summary>Gets stock records with optional zero/low/reserved filters.</summary>
    /// <param name="query">Validated pagination and product identifier search.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Stock page.</returns>
    Task<PagedResponse<StockItemResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    /// <summary>Computes aggregate stock health in the Inventory database.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Stock health totals.</returns>
    Task<InventorySummaryResponse> SummaryAsync(CancellationToken cancellationToken);
    /// <summary>Gets the latest bounded reservation history for a product.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The latest 100 reservations.</returns>
    Task<IReadOnlyCollection<ReservationResponse>> ReservationsAsync(Guid productId, CancellationToken cancellationToken);
    /// <summary>Stages an immutable stock-adjustment audit record in the same unit of work.</summary>
    /// <param name="adjustment">Validated adjustment.</param>
    void AddAdjustment(StockAdjustment adjustment);
}

/// <summary>Contains stock-health totals.</summary>
/// <param name="Products">Number of stock records.</param>
/// <param name="OnHand">Physical units.</param>
/// <param name="Reserved">Held units.</param>
/// <param name="Available">Unreserved units.</param>
/// <param name="LowStock">Records with at most five available units.</param>
public sealed record InventorySummaryResponse(int Products, long OnHand, long Reserved, long Available, int LowStock);

/// <summary>Contains a safe leased-reservation snapshot.</summary>
/// <param name="OrderId">Source order.</param>
/// <param name="Quantity">Held quantity.</param>
/// <param name="Status">Persisted reservation state.</param>
/// <param name="ExpiresAtUtc">Lease expiry.</param>
public sealed record ReservationResponse(Guid OrderId, int Quantity, string Status, DateTimeOffset ExpiresAtUtc);

/// <summary>Persists the reason and administrator for a stock adjustment.</summary>
public sealed class StockAdjustment
{
    /// <summary>Gets or sets the immutable audit identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Gets or sets the product identifier.</summary>
    public Guid ProductId { get; set; }
    /// <summary>Gets or sets the acting administrator.</summary>
    public Guid ActorId { get; set; }
    /// <summary>Gets or sets the signed unit change.</summary>
    public int Delta { get; set; }
    /// <summary>Gets or sets the required business reason.</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>Gets or sets the UTC operation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
