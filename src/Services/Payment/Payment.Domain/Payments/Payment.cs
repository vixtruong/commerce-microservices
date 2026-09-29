using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Payment.Domain.Payments;

/// <summary>Identifies a payment aggregate.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct PaymentId(Guid Value)
{
    /// <summary>Creates a new payment identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static PaymentId New() => new(Guid.NewGuid());
}

/// <summary>Defines the provider-processing lifecycle.</summary>
public enum PaymentStatus
{
    /// <summary>The request exists but has not started provider processing.</summary>
    Pending,
    /// <summary>A provider call is in progress.</summary>
    Processing,
    /// <summary>The provider approved the payment.</summary>
    Succeeded,
    /// <summary>The provider rejected or could not complete the payment.</summary>
    Failed,
    /// <summary>A previously successful payment was refunded.</summary>
    Refunded
}

/// <summary>Defines Payment aggregate failures.</summary>
public static class PaymentErrors
{
    /// <summary>Returned when payment input is invalid.</summary>
    public static readonly Error InvalidPayment = Error.Validation("Payment.Invalid", "Payment requires an order, positive amount, and three-letter currency.");
    /// <summary>Returned when the requested lifecycle transition is invalid.</summary>
    public static readonly Error InvalidTransition = Error.Conflict("Payment.InvalidTransition", "The payment state transition is invalid.");
}

/// <summary>
/// Tracks one idempotent payment attempt keyed by OrderId.
/// </summary>
public sealed class PaymentRecord : AggregateRoot<PaymentId>
{
    private PaymentRecord()
    {
    }

    private PaymentRecord(
        PaymentId id,
        Guid orderId,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the unique checkout order identifier.</summary>
    public Guid OrderId { get; private set; }
    /// <summary>Gets the authoritative requested amount.</summary>
    public decimal Amount { get; private set; }
    /// <summary>Gets the normalized currency.</summary>
    public string Currency { get; private set; } = string.Empty;
    /// <summary>Gets the payment lifecycle state.</summary>
    public PaymentStatus Status { get; private set; }
    /// <summary>Gets the external transaction reference on success.</summary>
    public string? TransactionReference { get; private set; }
    /// <summary>Gets the sanitized failure code on failure.</summary>
    public string? FailureCode { get; private set; }
    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC terminal time.</summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Creates a pending payment.</summary>
    /// <param name="orderId">Unique order identifier and idempotency key.</param>
    /// <param name="amount">Positive amount.</param>
    /// <param name="currency">Three-letter currency code.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <returns>A payment or validation error.</returns>
    public static Result<PaymentRecord> Create(
        Guid orderId,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty || amount <= 0 || string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return PaymentErrors.InvalidPayment;
        }

        return new PaymentRecord(PaymentId.New(), orderId, decimal.Round(amount, 2), currency.Trim().ToUpperInvariant(), createdAtUtc);
    }

    /// <summary>Marks provider processing as started.</summary>
    /// <returns>A success or invalid transition.</returns>
    public Result StartProcessing()
    {
        if (Status != PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Processing;
        return Result.Success();
    }

    /// <summary>Records a successful provider response.</summary>
    /// <param name="transactionReference">Non-sensitive provider reference.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result Succeed(string transactionReference, DateTimeOffset completedAtUtc)
    {
        if (Status != PaymentStatus.Processing || string.IsNullOrWhiteSpace(transactionReference))
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Succeeded;
        TransactionReference = transactionReference;
        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }

    /// <summary>Records a deterministic provider failure.</summary>
    /// <param name="failureCode">Sanitized machine-readable failure code.</param>
    /// <param name="completedAtUtc">UTC completion time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result Fail(string failureCode, DateTimeOffset completedAtUtc)
    {
        if (Status != PaymentStatus.Processing)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }
}
