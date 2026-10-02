using Commerce.BuildingBlocks.Application.Queries;

namespace Payment.Application.Payments;

/// <summary>Contains non-sensitive payments state.</summary>
/// <param name="Id">Payment identifier.</param>
/// <param name="OrderId">Source order.</param>
/// <param name="Amount">Authoritative charge amount.</param>
/// <param name="Currency">ISO currency.</param>
/// <param name="Status">Provider state.</param>
/// <param name="TransactionReference">Safe provider reference.</param>
/// <param name="FailureCode">Sanitized failure code.</param>
/// <param name="CreatedAtUtc">Creation time.</param>
/// <param name="CompletedAtUtc">Provider completion time.</param>
public sealed record PaymentResponse(Guid Id, Guid OrderId, decimal Amount, string Currency, string Status, string? TransactionReference, string? FailureCode, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);

/// <summary>Defines bounded payments read use cases.</summary>
public interface IPaymentReadStore
{
    /// <summary>Gets a bounded, filtered page.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe page records.</returns>
    Task<PagedResponse<PaymentResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    /// <summary>Gets one record by aggregate identifier.</summary>
    /// <param name="id">Aggregate identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe record or null.</returns>
    Task<PaymentResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Computes status counts in the owning database.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Counts and currency-separated totals.</returns>
    Task<PaymentSummaryResponse> SummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Contains operational status counts.</summary>
/// <param name="Total">All records.</param>
/// <param name="Statuses">Persisted status counts.</param>
/// <param name="Totals">Successful payment amount by currency, empty for Shipping.</param>
public sealed record PaymentSummaryResponse(int Total, IReadOnlyCollection<PaymentStatusCountResponse> Statuses, IReadOnlyCollection<CurrencyTotalResponse> Totals);

/// <summary>Counts a persisted state.</summary>
/// <param name="Status">State name.</param>
/// <param name="Count">Count.</param>
public sealed record PaymentStatusCountResponse(string Status, int Count);
