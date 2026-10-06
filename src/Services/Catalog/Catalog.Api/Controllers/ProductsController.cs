using Commerce.BuildingBlocks.Application.Security;
using System.ComponentModel.DataAnnotations;
using Catalog.Application.Products.Commands.CreateProduct;
using Catalog.Application.Products.Commands.UpdateProduct;
using Catalog.Application.Products.Queries.GetProductById;
using Catalog.Application.Products.Queries.GetProducts;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Exposes public REST operations for product catalog management and discovery.</summary>
[ApiController]
[Route("api/catalog/products")]
public sealed class ProductsController : ControllerBase
{
    /// <summary>Gets the stable route name used to generate product resource locations.</summary>
    public const string GetProductRouteName = "Catalog.GetProduct";

    private readonly ISender _sender;

    /// <summary>Initializes the products controller.</summary>
    /// <param name="sender">CQRS request sender.</param>
    public ProductsController(ISender sender) => _sender = sender;

    /// <summary>Gets a searchable, paged product list.</summary>
    /// <param name="search">Optional search term.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Page size from 1 through 100.</param>
    /// <param name="status">Optional publication filter.</param>
    /// <param name="sort">Allowed server sorting choice.</param>
    /// <param name="minPrice">Optional lower price.</param>
    /// <param name="maxPrice">Optional upper price.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>A product page.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ProductPageResponse>> GetProductsAsync(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string sort = "name",
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _sender.Send(new GetProductsQuery(search, page, pageSize, status, sort, minPrice, maxPrice), cancellationToken));

    /// <summary>Gets one product through the Redis cache-aside query.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The product or Problem Details.</returns>
    [HttpGet("{productId:guid}", Name = GetProductRouteName)]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        Result<ProductResponse> result = await _sender.Send(new GetProductByIdQuery(productId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }

    /// <summary>Creates a new draft product.</summary>
    /// <param name="request">Product creation request.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>A 201 response or Problem Details.</returns>
    [HttpPost]
    [Authorize(Policy = Permissions.ProductCreate)]
    public async Task<IActionResult> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        Result<CreateProductResponse> result = await _sender.Send(new CreateProductCommand(
            request.Sku, request.Name, request.Description, request.PriceAmount, request.PriceCurrency), cancellationToken);
        return result.IsSuccess
            // A named route is independent of MVC's default removal of the Async action-name suffix.
            ? CreatedAtRoute(GetProductRouteName, new { productId = result.Value.ProductId }, result.Value)
            : ProblemFor(result.Error);
    }

    /// <summary>Updates product content and price.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="request">Updated mutable values.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 or Problem Details.</returns>
    [HttpPut("{productId:guid}")]
    [Authorize(Policy = Permissions.ProductUpdate)]
    public async Task<IActionResult> UpdateProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        Result result = await _sender.Send(new UpdateProductCommand(
            productId, request.Name, request.Description, request.PriceAmount, request.PriceCurrency), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemFor(result.Error);
    }

    /// <summary>Activates a product for sale.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 or Problem Details.</returns>
    [HttpPost("{productId:guid}/activate")]
    [Authorize(Policy = Permissions.ProductUpdate)]
    public Task<IActionResult> ActivateAsync(Guid productId, CancellationToken cancellationToken) =>
        SetActivationAsync(productId, true, cancellationToken);

    /// <summary>Deactivates a product without deleting historical data.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 or Problem Details.</returns>
    [HttpPost("{productId:guid}/deactivate")]
    [Authorize(Policy = Permissions.ProductDeactivate)]
    public Task<IActionResult> DeactivateAsync(Guid productId, CancellationToken cancellationToken) =>
        SetActivationAsync(productId, false, cancellationToken);

    /// <summary>Applies an activation transition.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="activate">Target activation state.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 or Problem Details.</returns>
    private async Task<IActionResult> SetActivationAsync(Guid productId, bool activate, CancellationToken cancellationToken)
    {
        Result result = await _sender.Send(new SetProductActivationCommand(productId, activate), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemFor(result.Error);
    }

    /// <summary>Maps a domain/application error to safe Problem Details.</summary>
    /// <param name="error">Failure to map.</param>
    /// <returns>A typed HTTP error response.</returns>
    private ObjectResult ProblemFor(Error error) => Problem(
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        },
        title: error.Code,
        detail: error.Message);
}

/// <summary>Defines validated input for creating a product.</summary>
/// <param name="Sku">Unique SKU.</param>
/// <param name="Name">Product display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="PriceAmount">Non-negative price.</param>
/// <param name="PriceCurrency">Three-letter currency.</param>
public sealed record CreateProductRequest(
    [Required, StringLength(64)] string Sku,
    [Required, StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    [Range(typeof(decimal), "0", "999999999999.99")] decimal PriceAmount,
    [Required, RegularExpression("^[A-Za-z]{3}$")] string PriceCurrency);

/// <summary>Defines mutable product values.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Description.</param>
/// <param name="PriceAmount">Non-negative price.</param>
/// <param name="PriceCurrency">Three-letter currency.</param>
public sealed record UpdateProductRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    [Range(typeof(decimal), "0", "999999999999.99")] decimal PriceAmount,
    [Required, RegularExpression("^[A-Za-z]{3}$")] string PriceCurrency);
