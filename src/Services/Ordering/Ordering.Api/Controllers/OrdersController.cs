using Commerce.BuildingBlocks.Application.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Commerce.BuildingBlocks.Application.Queries;
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
    private readonly IOrderReadStore _reads;
    private readonly IAuthorizationService _authorization;

    /// <summary>Initializes the Orders controller.</summary>
    /// <param name="sender">CQRS request sender.</param>
    /// <param name="reads">Subject-scoped application read queries.</param>
    /// <param name="authorization">Standard resource authorization service.</param>
    public OrdersController(ISender sender, IOrderReadStore reads, IAuthorizationService authorization)
    {
        _sender = sender;
        _reads = reads;
        _authorization = authorization;
    }

    /// <summary>Gets only the authenticated customer's order history.</summary>
    /// <param name="query">Bounded filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Customer-owned order page.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> ListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _reads.ListAsync(query, CustomerId(), cancellationToken));

    /// <summary>Gets an administrative order page.</summary>
    /// <param name="query">Bounded filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Operational order page.</returns>
    [HttpGet("admin")]
    [Authorize(Policy = Permissions.OrderRead)]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> AdminListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _reads.ListAsync(query, null, cancellationToken));

    /// <summary>Gets an order in the authorized administrative scope.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Order snapshot or 404.</returns>
    [HttpGet("admin/{orderId:guid}")]
    [Authorize(Policy = Permissions.OrderRead)]
    public async Task<ActionResult<OrderResponse>> AdminGetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        OrderResponse? response = await _reads.GetAsync(orderId, null, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Gets server-computed operational summary counts.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Currency-separated paid order totals and counts.</returns>
    [HttpGet("summary")]
    [Authorize(Policy = Permissions.OrderRead)]
    public async Task<ActionResult<OrderSummaryResponse>> SummaryAsync(CancellationToken cancellationToken) =>
        Ok(await _reads.SummaryAsync(cancellationToken));

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
        OrderResponse? response = await _reads.GetAsync(orderId, null, cancellationToken);
        if (response is null) return NotFound();
        // Authorize the persisted owner rather than trusting a client-supplied customer identifier.
        AuthorizationResult authorized = await _authorization.AuthorizeAsync(User,
            new OwnedResource(response.CustomerId), Permissions.OrderResourcePolicy);
        return authorized.Succeeded ? Ok(response) : NotFound();
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
    [Required, StringLength(200)] string RecipientName,
    [Required, StringLength(300)] string AddressLine1,
    [Required, StringLength(100)] string City,
    [Required, StringLength(30)] string PostalCode,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string CountryCode);
