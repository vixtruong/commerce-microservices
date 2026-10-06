using System.ComponentModel.DataAnnotations;
using Catalog.Application.Categories;
using Commerce.BuildingBlocks.Application.Security;
using Commerce.BuildingBlocks.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Exposes public product groups and permission-protected group administration through YARP.</summary>
[ApiController]
[Route("api/catalog/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    private readonly IAuthorizationService _authorization;

    /// <summary>Initializes group discovery and administration.</summary>
    /// <param name="categories">Catalog application use cases.</param>
    /// <param name="authorization">Backend permission enforcement.</param>
    public CategoriesController(ICategoryService categories, IAuthorizationService authorization)
    {
        _categories = categories;
        _authorization = authorization;
    }

    /// <summary>Gets active groups, or the administrative list including disabled groups.</summary>
    /// <param name="includeInactive">Requires product-update permission when true.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Group snapshots with active product counts.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyCollection<CategoryResponse>>> ListAsync(
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        // The public storefront cannot request disabled administrative groups by changing a query parameter.
        if (includeInactive && !(await _authorization.AuthorizeAsync(User, Permissions.ProductUpdate)).Succeeded)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        return Ok(await _categories.ListAsync(includeInactive, cancellationToken));
    }

    /// <summary>Creates a stable product group.</summary>
    /// <param name="request">Validated name and slug.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>201 or safe Problem Details.</returns>
    [HttpPost]
    [Authorize(Policy = Permissions.ProductCreate)]
    public async Task<IActionResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        Result<CategoryResponse> result = await _categories.CreateAsync(request.Name, request.Slug, cancellationToken);
        return result.IsSuccess ? Created($"/api/catalog/categories?includeInactive=false", result.Value) : ProblemFor(result.Error);
    }

    /// <summary>Updates a group's name and assignment availability, preserving its slug.</summary>
    /// <param name="slug">Stable group slug.</param>
    /// <param name="request">Display name and availability.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>204 or safe Problem Details.</returns>
    [HttpPut("{slug}")]
    [Authorize(Policy = Permissions.ProductUpdate)]
    public async Task<IActionResult> UpdateAsync(string slug, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        Result result = await _categories.UpdateAsync(slug, request.Name, request.IsActive, cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemFor(result.Error);
    }

    /// <summary>Maps category failures to safe HTTP responses.</summary>
    /// <param name="error">Application failure.</param>
    /// <returns>Problem Details.</returns>
    private ObjectResult ProblemFor(Error error) => Problem(statusCode: error.Type switch
    {
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        _ => 400
    }, title: error.Code, detail: error.Message);
}

/// <summary>Defines group creation input.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Slug">Unique lowercase URL-safe slug.</param>
public sealed record CreateCategoryRequest([Required, StringLength(120)] string Name,
    [Required, StringLength(120), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$")] string Slug);

/// <summary>Defines group administration input.</summary>
/// <param name="Name">Display name.</param>
/// <param name="IsActive">Whether new assignments are allowed.</param>
public sealed record UpdateCategoryRequest([Required, StringLength(120)] string Name, bool IsActive);
