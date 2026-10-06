using Commerce.BuildingBlocks.Application.Security;
using Commerce.BuildingBlocks.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.Payments;

namespace Payment.Api.Controllers;

/// <summary>Exposes authorized payments operational read use cases.</summary>
[ApiController]
[Authorize(Policy = Permissions.PaymentRead)]
[Route("api/payments")]
public sealed class PaymentOperationsController : ControllerBase
{
    private readonly IPaymentReadStore _reads;
    /// <summary>Initializes operational queries.</summary>
    /// <param name="reads">Application read port.</param>
    public PaymentOperationsController(IPaymentReadStore reads) => _reads = reads;

    /// <summary>Gets a bounded operational page.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Page records.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PaymentResponse>>> ListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _reads.ListAsync(query, cancellationToken));

    /// <summary>Gets operational counts without downloading full tables.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Summary counts.</returns>
    [HttpGet("summary")]
    public async Task<ActionResult<PaymentSummaryResponse>> SummaryAsync(CancellationToken cancellationToken) =>
        Ok(await _reads.SummaryAsync(cancellationToken));

    /// <summary>Gets a payment by identifier.</summary>
    /// <param name="id">Payment identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Payment or 404.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        PaymentResponse? row = await _reads.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

}
