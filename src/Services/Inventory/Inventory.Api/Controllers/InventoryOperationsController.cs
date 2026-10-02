using Commerce.BuildingBlocks.Application.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Commerce.BuildingBlocks.Application.Queries;
using Commerce.BuildingBlocks.Domain.Results;
using Inventory.Application.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Controllers;

/// <summary>Exposes bounded stock operations and a deliberately limited public availability query.</summary>
[ApiController]
[Route("api/inventory")]
public sealed class InventoryOperationsController : ControllerBase
{
    private readonly IInventoryReadStore _reads;
    private readonly InventoryService _inventory;
    /// <summary>Initializes Inventory operations.</summary>
    /// <param name="reads">Inventory read port.</param>
    /// <param name="inventory">Guarded domain use cases.</param>
    public InventoryOperationsController(IInventoryReadStore reads, InventoryService inventory) { _reads = reads; _inventory = inventory; }

    /// <summary>Gets a bounded administrative stock overview.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Stock page.</returns>
    [HttpGet]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<ActionResult<PagedResponse<StockItemResponse>>> ListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _reads.ListAsync(query, cancellationToken));

    /// <summary>Gets aggregate stock health.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Stock health totals.</returns>
    [HttpGet("summary")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<ActionResult<InventorySummaryResponse>> SummaryAsync(CancellationToken cancellationToken) =>
        Ok(await _reads.SummaryAsync(cancellationToken));

    /// <summary>Gets available units without exposing reservation details.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Available units, zero for a product with no stock record.</returns>
    [HttpGet("{productId:guid}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> AvailabilityAsync(Guid productId, CancellationToken cancellationToken)
    {
        StockItemResponse? stock = await _inventory.GetAsync(productId, cancellationToken);
        return Ok(new { productId, availableQuantity = stock?.AvailableQuantity ?? 0 });
    }

    /// <summary>Gets a bounded reservation history for operational diagnosis.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The latest 100 persisted reservations.</returns>
    [HttpGet("{productId:guid}/reservations")]
    [Authorize(Policy = Permissions.ReservationRead)]
    public async Task<ActionResult<IReadOnlyCollection<ReservationResponse>>> ReservationsAsync(Guid productId, CancellationToken cancellationToken) =>
        Ok(await _reads.ReservationsAsync(productId, cancellationToken));

    /// <summary>Applies an audited physical-stock adjustment while preserving all held units.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="request">Delta, reason, and observed concurrency version.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Updated stock, validation failure, or conflict requiring a fresh read.</returns>
    [HttpPost("{productId:guid}/adjustments")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<ActionResult<StockItemResponse>> AdjustAsync(Guid productId, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out Guid actor)) return Unauthorized();
        try
        {
            Result<StockItemResponse> result = await _inventory.AdjustAsync(productId, request.Delta, request.Reason,
                request.Version, actor, _reads, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : Problem(statusCode: result.Error.Type == ErrorType.Conflict ? 409 : 400,
                title: result.Error.Code, detail: result.Error.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A Saga mutation can race the administrator after the version check; EF remains the final guard.
            return Problem(statusCode: 409, title: "Inventory.Concurrency", detail: "Stock changed. Refresh and review the adjustment.");
        }
    }
}

/// <summary>Defines a validated signed stock adjustment.</summary>
/// <param name="Delta">Signed physical-unit change.</param>
/// <param name="Reason">Required audit reason.</param>
/// <param name="Version">Observed aggregate concurrency version, zero for a first receipt.</param>
public sealed record AdjustStockRequest(int Delta, [Required, MinLength(3), MaxLength(500)] string Reason, [Range(0, long.MaxValue)] long Version);
