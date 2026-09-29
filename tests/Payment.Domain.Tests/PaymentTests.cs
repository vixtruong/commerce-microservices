using Microsoft.Extensions.Options;
using Payment.Application.Payments;
using Payment.Domain.Payments;
using Payment.Infrastructure.Payments;

namespace Payment.Domain.Tests;

/// <summary>Verifies Payment lifecycle rules and deterministic fake-provider behavior.</summary>
public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies successful aggregate completion.</summary>
    [Fact]
    public void Succeed_ProcessingPayment_StoresReference()
    {
        PaymentRecord payment = PaymentRecord.Create(Guid.NewGuid(), 10m, "USD", Now).Value;
        payment.StartProcessing();

        var result = payment.Succeed("tx-1", Now.AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("tx-1", payment.TransactionReference);
    }

    /// <summary>Verifies failed aggregate completion.</summary>
    [Fact]
    public void Fail_ProcessingPayment_StoresFailureCode()
    {
        PaymentRecord payment = PaymentRecord.Create(Guid.NewGuid(), 10m, "USD", Now).Value;
        payment.StartProcessing();

        payment.Fail("declined", Now.AddSeconds(1));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("declined", payment.FailureCode);
    }

    /// <summary>Verifies deterministic provider success without random test behavior.</summary>
    [Fact]
    public async Task FakeGateway_ConfiguredSuccess_ReturnsApproval()
    {
        var gateway = new FakePaymentGateway(Options.Create(new FakePaymentOptions { Outcome = FakePaymentOutcome.Success }));

        PaymentGatewayResult result = await gateway.ChargeAsync(Guid.NewGuid(), 10m, "USD", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TransactionReference);
    }

    /// <summary>Verifies deterministic provider failure without random test behavior.</summary>
    [Fact]
    public async Task FakeGateway_ConfiguredFailure_ReturnsRejection()
    {
        var gateway = new FakePaymentGateway(Options.Create(new FakePaymentOptions { Outcome = FakePaymentOutcome.Failure }));

        PaymentGatewayResult result = await gateway.ChargeAsync(Guid.NewGuid(), 10m, "USD", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("fake_declined", result.FailureCode);
    }

    /// <summary>Verifies deterministic provider timeout observes the caller's bounded token.</summary>
    [Fact]
    public async Task FakeGateway_ConfiguredTimeout_ThrowsCancellation()
    {
        var gateway = new FakePaymentGateway(Options.Create(new FakePaymentOptions { Outcome = FakePaymentOutcome.Timeout }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.ChargeAsync(Guid.NewGuid(), 10m, "USD", timeout.Token));
    }
}
