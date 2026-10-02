using Commerce.BuildingBlocks.Application.Security;
using Commerce.BuildingBlocks.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shipping.Application.Shipments;

namespace Shipping.Api.Controllers;

/// <summary>Exposes authorized shipping operational read use cases.</summary>
[ApiController]
[Authorize(Policy = Permissions.ShipmentRead)]
[Route("api/shipping")]
public sealed class ShippingOperationsController : ControllerBase
{
    private readonly IShippingReadStore _reads;
    /// <summary>Initializes operational queries.</summary>
    /// <param name="reads">Application read port.</param>
    public ShippingOperationsController(IShippingReadStore reads) => _reads = reads;

    /// <summary>Gets a bounded operational page.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Page records.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ShipmentResponse>>> ListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _reads.ListAsync(query, cancellationToken));

    /// <summary>Gets operational counts without downloading full tables.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Summary counts.</returns>
    [HttpGet("summary")]
    public async Task<ActionResult<ShippingSummaryResponse>> SummaryAsync(CancellationToken cancellationToken) =>
        Ok(await _reads.SummaryAsync(cancellationToken));

}
