using System.Security.Claims;
using Cart.Application.Carts;
using Commerce.BuildingBlocks.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cart.Api.Controllers;

/// <summary>Manages only the cart belonging to the authenticated customer claim.</summary>
[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController : ControllerBase
{
    private readonly CartService _cart;

    /// <summary>Initializes the cart controller.</summary>
    /// <param name="cart">Cart application service.</param>
    public CartController(CartService cart) => _cart = cart;

    /// <summary>Gets the authenticated customer's cart.</summary>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The cart.</returns>
    [HttpGet]
    public async Task<ActionResult<CustomerCart>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await _cart.GetAsync(CustomerId(), cancellationToken));

    /// <summary>Adds or changes an item quantity.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="request">Positive desired quantity.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The updated cart or Problem Details.</returns>
    [HttpPut("items/{productId:guid}")]
    public async Task<ActionResult<CustomerCart>> SetItemAsync(Guid productId, SetCartItemRequest request, CancellationToken cancellationToken)
    {
        Result<CustomerCart> result = await _cart.SetItemAsync(CustomerId(), productId, request.Quantity, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: result.Error.Type == ErrorType.Validation ? 400 : 409, title: result.Error.Code, detail: result.Error.Message);
    }

    /// <summary>Removes one item.</summary>
    /// <param name="productId">Product to remove.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The updated cart or 404.</returns>
    [HttpDelete("items/{productId:guid}")]
    public async Task<ActionResult<CustomerCart>> RemoveItemAsync(Guid productId, CancellationToken cancellationToken)
    {
        Result<CustomerCart> result = await _cart.RemoveItemAsync(CustomerId(), productId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Clears the authenticated customer's cart.</summary>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await _cart.ClearAsync(CustomerId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Reads the trusted customer identifier from the validated JWT subject claim.</summary>
    /// <returns>The authenticated customer identifier.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when a token lacks a valid subject.</exception>
    private Guid CustomerId() =>
        Guid.TryParse(User.FindFirstValue("sub"), out Guid customerId)
            ? customerId
            : throw new UnauthorizedAccessException("The access token subject is invalid.");
}

/// <summary>Defines an add/change cart item request.</summary>
/// <param name="Quantity">Positive desired quantity.</param>
public sealed record SetCartItemRequest(int Quantity);
