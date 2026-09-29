using System.Diagnostics;
using Microsoft.Extensions.Options;
using Payment.Application.Payments;

namespace Payment.Infrastructure.Payments;

/// <summary>
/// Provides deterministic development payment behavior without external credentials.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly FakePaymentOptions _options;

    /// <summary>Initializes the fake provider.</summary>
    /// <param name="options">Deterministic outcome configuration.</param>
    public FakePaymentGateway(IOptions<FakePaymentOptions> options) => _options = options.Value;

    /// <inheritdoc />
    public async Task<PaymentGatewayResult> ChargeAsync(
        Guid orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        return _options.Outcome switch
        {
            FakePaymentOutcome.Success => new PaymentGatewayResult(
                true,
                $"fake-{orderId:N}",
                null),
            FakePaymentOutcome.Failure => new PaymentGatewayResult(false, null, "fake_declined"),
            FakePaymentOutcome.Timeout => await WaitForTimeoutAsync(cancellationToken),
            _ => throw new InvalidOperationException("Unsupported fake payment outcome.")
        };
    }

    /// <summary>Waits only until the bounded caller timeout is observed.</summary>
    /// <param name="cancellationToken">Required timeout token.</param>
    /// <returns>This method does not return normally.</returns>
    private static async Task<PaymentGatewayResult> WaitForTimeoutAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new UnreachableException();
    }
}
