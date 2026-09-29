using System.Security.Claims;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Ordering.Application.Orders;

namespace Ordering.Api.Controllers;

/// <summary>Starts eventually consistent checkout and exposes customer-owned order state.</summary>
[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    /// <summary>Gets the stable route name used to generate order resource locations.</summary>
    public const string GetOrderRouteName = "Ordering.GetOrder";

    private readonly ISender _sender;

    /// <summary>Initializes the Orders controller.</summary>
    /// <param name="sender">CQRS request sender.</param>
    public OrdersController(ISender sender) => _sender = sender;

    /// <summary>
    /// Creates an order locally and returns immediately; Inventory, Payment, and Shipping continue asynchronously.
    /// </summary>
    /// <param name="request">Shipping address request.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>202 Accepted, a replayed response, or Problem Details.</returns>
    [HttpPost("checkout")]
    public async Task<IActionResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        string idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        Result<CheckoutResponse> result = await _sender.Send(new CheckoutCommand(
            CustomerId(), idempotencyKey, request.RecipientName, request.AddressLine1,
            request.City, request.PostalCode, request.CountryCode), cancellationToken);
        return result.IsSuccess
            // The stable route name keeps Location generation independent of C# method names.
            ? AcceptedAtRoute(GetOrderRouteName, new { orderId = result.Value.OrderId }, result.Value)
            : ProblemFor(result.Error);
    }

    /// <summary>Gets the current eventually consistent order state.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>The order or 404.</returns>
    [HttpGet("{orderId:guid}", Name = GetOrderRouteName)]
    public async Task<ActionResult<OrderResponse>> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Result<OrderResponse> result = await _sender.Send(new GetOrderQuery(orderId, CustomerId()), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Reads the trusted customer identifier from the validated JWT subject claim.</summary>
    /// <returns>The authenticated customer identifier.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the token lacks a valid subject.</exception>
    private Guid CustomerId() => Guid.TryParse(User.FindFirstValue("sub"), out Guid customerId)
        ? customerId
        : throw new UnauthorizedAccessException("The access token subject is invalid.");

    /// <summary>Maps safe application failures to standard Problem Details.</summary>
    /// <param name="error">Application error.</param>
    /// <returns>An HTTP error response.</returns>
    private ObjectResult ProblemFor(Error error) => Problem(
        statusCode: error.Type == ErrorType.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
        title: error.Code,
        detail: error.Message);
}

/// <summary>Defines the shipping address captured at checkout.</summary>
/// <param name="RecipientName">Recipient name.</param>
/// <param name="AddressLine1">Primary address line.</param>
/// <param name="City">City.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">Two-letter country code.</param>
public sealed record CheckoutRequest(
    string RecipientName,
    string AddressLine1,
    string City,
    string PostalCode,
    string CountryCode);
