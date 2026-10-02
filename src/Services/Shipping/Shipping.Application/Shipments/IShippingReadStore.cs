using Commerce.BuildingBlocks.Application.Queries;

namespace Shipping.Application.Shipments;

/// <summary>Contains non-sensitive shipping state.</summary>
/// <param name="Id">Shipment identifier.</param>
/// <param name="OrderId">Source order.</param>
/// <param name="TrackingNumber">Tracking identifier.</param>
/// <param name="Status">Fulfilment state.</param>
/// <param name="CreatedAtUtc">Creation time.</param>
/// <param name="UpdatedAtUtc">Latest transition time.</param>
public sealed record ShipmentResponse(Guid Id, Guid OrderId, string TrackingNumber, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Defines bounded shipping read use cases.</summary>
public interface IShippingReadStore
{
    /// <summary>Gets a bounded, filtered page.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe page records.</returns>
    Task<PagedResponse<ShipmentResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    /// <summary>Gets one record by aggregate identifier.</summary>
    /// <param name="id">Aggregate identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe record or null.</returns>
    Task<ShipmentResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Computes status counts in the owning database.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Counts and currency-separated totals.</returns>
    Task<ShippingSummaryResponse> SummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Contains operational status counts.</summary>
/// <param name="Total">All records.</param>
/// <param name="Statuses">Persisted status counts.</param>
/// <param name="Totals">Successful payment amount by currency, empty for Shipping.</param>
public sealed record ShippingSummaryResponse(int Total, IReadOnlyCollection<ShippingStatusCountResponse> Statuses, IReadOnlyCollection<CurrencyTotalResponse> Totals);

/// <summary>Counts a persisted state.</summary>
/// <param name="Status">State name.</param>
/// <param name="Count">Count.</param>
public sealed record ShippingStatusCountResponse(string Status, int Count);
