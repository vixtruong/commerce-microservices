using Commerce.BuildingBlocks.Application.Security;
using Commerce.BuildingBlocks.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shipping.Application.Shipments;
using Shipping.Domain.Shipments;

namespace Shipping.Api.Controllers;

/// <summary>Queries shipment state and advances fake fulfilment in development.</summary>
[ApiController]
[Authorize(Policy = Permissions.ShipmentRead)]
[Route("api/shipping")]
public sealed class ShippingController : ControllerBase
{
    private readonly ShippingService _shipping;

    /// <summary>Initializes the Shipping controller.</summary>
    /// <param name="shipping">Shipping application service.</param>
    public ShippingController(ShippingService shipping) => _shipping = shipping;

    /// <summary>Gets a shipment by identifier.</summary>
    /// <param name="shipmentId">Shipment identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The shipment or 404.</returns>
    [HttpGet("{shipmentId:guid}")]
    public async Task<IActionResult> GetAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        Shipment? shipment = await _shipping.GetAsync(shipmentId, cancellationToken);
        return shipment is null ? NotFound() : Ok(ToResponse(shipment));
    }

    /// <summary>Gets shipment state by the source order identifier.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The shipment or 404 while asynchronous creation is pending.</returns>
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Shipment? shipment = await _shipping.GetByOrderIdAsync(orderId, cancellationToken);
        return shipment is null ? NotFound() : Ok(ToResponse(shipment));
    }

    /// <summary>Advances the deterministic fake shipment by one state.</summary>
    /// <param name="shipmentId">Shipment identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The updated shipment or Problem Details.</returns>
    [HttpPost("{shipmentId:guid}/advance")]
    [Authorize(Policy = Permissions.ShipmentUpdate)]
    public async Task<IActionResult> AdvanceAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        Result<Shipment> result = await _shipping.AdvanceAsync(shipmentId, cancellationToken);
        return result.IsSuccess
            ? Ok(ToResponse(result.Value))
            : Problem(statusCode: result.Error.Type == ErrorType.NotFound ? 404 : 409, title: result.Error.Code, detail: result.Error.Message);
    }

    /// <summary>Projects non-sensitive shipment state.</summary>
    /// <param name="shipment">Shipment aggregate.</param>
    /// <returns>Anonymous response object.</returns>
    private static object ToResponse(Shipment shipment) => new
    {
        id = shipment.Id.Value,
        shipment.OrderId,
        shipment.TrackingNumber,
        status = shipment.Status.ToString(),
        shipment.CreatedAtUtc,
        shipment.UpdatedAtUtc
    };
}
