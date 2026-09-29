namespace Payment.Application.Payments;

/// <summary>Defines the result of one provider authorization attempt.</summary>
/// <param name="Succeeded">Whether the provider approved the payment.</param>
/// <param name="TransactionReference">Provider reference when approved.</param>
/// <param name="FailureCode">Sanitized failure code when rejected.</param>
public sealed record PaymentGatewayResult(bool Succeeded, string? TransactionReference, string? FailureCode);

/// <summary>Abstracts an external payment provider from application orchestration.</summary>
public interface IPaymentGateway
{
    /// <summary>Attempts to charge one order with a bounded cancellation token.</summary>
    /// <param name="orderId">Unique order idempotency key.</param>
    /// <param name="amount">Positive amount.</param>
    /// <param name="currency">Three-letter currency.</param>
    /// <param name="cancellationToken">Provider timeout and cancellation token.</param>
    /// <returns>A deterministic success or failure result.</returns>
    Task<PaymentGatewayResult> ChargeAsync(
        Guid orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);
}

/// <summary>Defines deterministic behavior for the development payment provider.</summary>
public enum FakePaymentOutcome
{
    /// <summary>Approve the payment.</summary>
    Success,
    /// <summary>Reject the payment.</summary>
    Failure,
    /// <summary>Wait until the caller's timeout cancels the request.</summary>
    Timeout
}

/// <summary>Configures the deterministic fake payment adapter.</summary>
public sealed class FakePaymentOptions
{
    /// <summary>Gets or sets the configured provider outcome.</summary>
    public FakePaymentOutcome Outcome { get; set; } = FakePaymentOutcome.Success;
}
