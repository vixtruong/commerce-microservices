using Commerce.BuildingBlocks.Application.Queries;

namespace Ordering.Application.Orders;

/// <summary>Defines subject-scoped and administrative Order read use cases.</summary>
public interface IOrderReadStore
{
    /// <summary>Gets a bounded page; a subject filter is mandatory for customer callers.</summary>
    /// <param name="query">Validated pagination and filters.</param>
    /// <param name="customerId">Customer subject; null is permitted only for an authorized administrator.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe order snapshots.</returns>
    Task<PagedResponse<OrderResponse>> ListAsync(PageQuery query, Guid? customerId, CancellationToken cancellationToken);

    /// <summary>Gets one order without crossing database ownership boundaries.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="customerId">Optional administrative scope.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The safe order snapshot or null.</returns>
    Task<OrderResponse?> GetAsync(Guid orderId, Guid? customerId, CancellationToken cancellationToken);

    /// <summary>Computes operational counts and currency-separated paid totals in PostgreSQL.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Operational summary.</returns>
    Task<OrderSummaryResponse> SummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Contains persisted workflow counts and paid order value by currency.</summary>
/// <param name="Total">All orders.</param>
/// <param name="Processing">Orders waiting on checkout.</param>
/// <param name="Cancelled">Cancelled orders.</param>
/// <param name="PaidTotals">Paid order value; not settled provider revenue.</param>
/// <param name="Statuses">Counts per persisted order status.</param>
public sealed record OrderSummaryResponse(int Total, int Processing, int Cancelled,
    IReadOnlyCollection<CurrencyTotalResponse> PaidTotals, IReadOnlyCollection<StatusCountResponse> Statuses);

/// <summary>Counts a persisted order state.</summary>
/// <param name="Status">State name.</param>
/// <param name="Count">Number of orders.</param>
public sealed record StatusCountResponse(string Status, int Count);
