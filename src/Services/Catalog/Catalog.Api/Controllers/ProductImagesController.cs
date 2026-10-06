using Catalog.Application.Images;
using Commerce.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Accepts authorized product photos through the public Gateway.</summary>
[ApiController]
[Authorize]
[Route("api/catalog/images")]
public sealed class ProductImagesController : ControllerBase
{
    private readonly IProductImageService _images;
    private readonly IAuthorizationService _authorization;

    /// <summary>Initializes product photo uploads.</summary>
    /// <param name="images">Validated image use cases.</param>
    /// <param name="authorization">Catalog permission checks.</param>
    public ProductImagesController(IProductImageService images, IAuthorizationService authorization)
    {
        _images = images;
        _authorization = authorization;
    }

    /// <summary>Stores a photo for a product creator or editor; saving the product assigns it.</summary>
    /// <param name="file">JPEG, PNG or WebP multipart file.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>201 with the immutable media path, 400 for invalid photos, or 403 without permission.</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ActionResult<ProductImageResponse>> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        // Either existing Catalog write capability authorizes uploads; a customer cannot store files.
        if (!(await _authorization.AuthorizeAsync(User, Permissions.ProductCreate)).Succeeded &&
            !(await _authorization.AuthorizeAsync(User, Permissions.ProductUpdate)).Succeeded) return Forbid();
        await using Stream stream = file.OpenReadStream();
        var result = await _images.UploadAsync(stream, file.ContentType, file.Length, cancellationToken);
        return result.IsSuccess ? Created(result.Value.ImageUrl, result.Value) :
            Problem(statusCode: 400, title: result.Error.Code, detail: result.Error.Message);
    }
}
