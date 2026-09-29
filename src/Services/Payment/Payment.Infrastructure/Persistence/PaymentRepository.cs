using Microsoft.EntityFrameworkCore;
using Payment.Application.Payments;
using Payment.Domain.Payments;

namespace Payment.Infrastructure.Persistence;

/// <summary>Implements Payment persistence and OrderId idempotency lookup.</summary>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    /// <summary>Initializes the repository.</summary>
    /// <param name="dbContext">Payment context.</param>
    public PaymentRepository(PaymentDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<PaymentRecord> query = _dbContext.Payments;
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);
    }

    /// <inheritdoc />
    public void Add(PaymentRecord payment) => _dbContext.Payments.Add(payment);
}
