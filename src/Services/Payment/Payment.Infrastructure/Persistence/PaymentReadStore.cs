using Commerce.BuildingBlocks.Application.Queries;
using Microsoft.EntityFrameworkCore;
using Payment.Application.Payments;
using Payment.Domain.Payments;

namespace Payment.Infrastructure.Persistence;

/// <summary>Queries only the Payment-owned database with bounded pagination.</summary>
public sealed class PaymentReadStore : IPaymentReadStore
{
    private readonly PaymentDbContext _db;
    /// <summary>Initializes the read store.</summary>
    /// <param name="db">Payment database.</param>
    public PaymentReadStore(PaymentDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PagedResponse<PaymentResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        IQueryable<PaymentRecord> rows = _db.Payments.AsNoTracking();
        if (query.Status is not null && Enum.TryParse(query.Status, out PaymentStatus status)) rows = rows.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            if (Guid.TryParse(query.Search, out Guid id)) rows = rows.Where(p => p.OrderId == id || p.Id == new PaymentId(id));
            else rows = rows.Where(p => false);
        }
        if (query.From.HasValue) rows = rows.Where(p => p.CreatedAtUtc >= query.From.Value);
        if (query.To.HasValue) rows = rows.Where(p => p.CreatedAtUtc < query.To.Value);
        int total = await rows.CountAsync(cancellationToken);
        PaymentRecord[] page = await rows.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new(page.Select(Map).ToArray(), query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<PaymentResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        PaymentRecord? row = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Id == new PaymentId(id), cancellationToken);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<PaymentSummaryResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await _db.Payments.AsNoTracking().GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(cancellationToken);
        var amounts = await _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Succeeded)
            .GroupBy(p => p.Currency).Select(g => new CurrencyTotalResponse(g.Key, g.Sum(p => p.Amount))).ToArrayAsync(cancellationToken);
        return new(counts.Sum(c => c.Count), counts.Select(c => new PaymentStatusCountResponse(c.Status.ToString(), c.Count)).ToArray(), amounts);
    }

    /// <summary>Projects a persistence aggregate into its public read contract.</summary>
    /// <param name="p">Read-only aggregate.</param>
    /// <returns>Safe response.</returns>
    private static PaymentResponse Map(PaymentRecord p) => new(p.Id.Value, p.OrderId, p.Amount, p.Currency, p.Status.ToString(), p.TransactionReference, p.FailureCode, p.CreatedAtUtc, p.CompletedAtUtc);
}
