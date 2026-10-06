using Commerce.BuildingBlocks.Domain.Results;
using MediatR;

namespace Catalog.Application.Products.Commands.CreateProduct
{
    /// <summary>
    /// Requests creation of a Catalog product.
    /// </summary>
    /// <param name="Sku">Unique product SKU.</param>
    /// <param name="Name">Product display name.</param>
    /// <param name="Description">Optional product description.</param>
    /// <param name="PriceAmount">Initial price amount.</param>
    /// <param name="PriceCurrency">Three-character currency code.</param>
    /// <param name="Brand">Optional manufacturer.</param>
    /// <param name="ImageUrl">Optional Catalog media path.</param>
    /// <param name="SourceUrl">Optional manufacturer reference.</param>
    /// <param name="CategorySlug">Optional active category assignment.</param>
    /// <param name="ImageUrls">Optional ordered gallery, up to eight photos.</param>
    public sealed record CreateProductCommand(
        string Sku,
        string Name,
        string? Description,
        decimal PriceAmount,
        string PriceCurrency, string? Brand = null, string? ImageUrl = null,
        string? SourceUrl = null, string? CategorySlug = null, IReadOnlyCollection<string>? ImageUrls = null)
        : IRequest<Result<CreateProductResponse>>;

    /// <summary>
    /// Represents the result returned after creating a product.
    /// </summary>
    /// <param name="ProductId">Identifier of the created product.</param>
    public sealed record CreateProductResponse(Guid ProductId);

}
