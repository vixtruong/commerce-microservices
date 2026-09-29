using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.Payments;
using Payment.Domain.Payments;

namespace Payment.Api.Controllers;

/// <summary>Provides an administrative read endpoint for fake-provider demonstration.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentRepository _payments;

    /// <summary>Initializes the payments controller.</summary>
    /// <param name="payments">Payment repository.</param>
    public PaymentsController(IPaymentRepository payments) => _payments = payments;

    /// <summary>Gets payment state by order identifier.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The payment state or 404.</returns>
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        PaymentRecord? payment = await _payments.GetByOrderIdAsync(orderId, tracked: false, cancellationToken);
        return payment is null
            ? NotFound()
            : Ok(new
            {
                id = payment.Id.Value,
                payment.OrderId,
                payment.Amount,
                payment.Currency,
                status = payment.Status.ToString(),
                payment.TransactionReference,
                payment.CreatedAtUtc,
                payment.CompletedAtUtc
            });
    }
}
