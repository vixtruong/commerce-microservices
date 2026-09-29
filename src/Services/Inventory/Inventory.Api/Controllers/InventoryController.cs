using Commerce.BuildingBlocks.Domain.Results;
using Inventory.Application.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes Inventory queries and protected stock-receipt administration.</summary>
[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly InventoryService _inventory;

    /// <summary>Initializes the Inventory controller.</summary>
    /// <param name="inventory">Inventory application service.</param>
    public InventoryController(InventoryService inventory) => _inventory = inventory;

    /// <summary>Gets current stock state for a product.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Stock state or 404.</returns>
    [HttpGet("{productId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StockItemResponse>> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        StockItemResponse? response = await _inventory.GetAsync(productId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Receives physical stock for a product.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="request">Positive receipt quantity.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Updated stock state or Problem Details.</returns>
    [HttpPost("{productId:guid}/receipts")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StockItemResponse>> IncreaseAsync(
        Guid productId,
        IncreaseStockRequest request,
        CancellationToken cancellationToken)
    {
        Result<StockItemResponse> result = await _inventory.IncreaseAsync(productId, request.Quantity, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error.Code, detail: result.Error.Message);
    }
}

/// <summary>Defines a stock receipt request.</summary>
/// <param name="Quantity">Positive quantity received.</param>
public sealed record IncreaseStockRequest(int Quantity);
