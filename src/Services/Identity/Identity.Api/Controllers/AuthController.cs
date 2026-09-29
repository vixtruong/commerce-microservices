using System.ComponentModel.DataAnnotations;
using Identity.Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>Exposes registration, login, refresh rotation, and logout/revocation.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IIdentityService _identity;

    /// <summary>Initializes the authentication controller.</summary>
    /// <param name="identity">Secure Identity application service.</param>
    public AuthController(IIdentityService identity) => _identity = identity;

    /// <summary>Registers a customer using ASP.NET Core Identity password hashing.</summary>
    /// <param name="request">Registration credentials.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>201 tokens or safe validation Problem Details.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        AuthenticationResult<TokenResponse> result = await _identity.RegisterAsync(request.Email, request.Password, cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Problem(statusCode: 400, title: "Identity.RegistrationFailed", detail: "Registration could not be completed.");
    }

    /// <summary>Authenticates a customer and issues short-lived access credentials.</summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Tokens or 401 without credential detail leakage.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        AuthenticationResult<TokenResponse> result = await _identity.LoginAsync(request.Email, request.Password, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }

    /// <summary>Rotates a valid refresh token and revokes the old token.</summary>
    /// <param name="request">Opaque refresh token.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Replacement tokens or 401.</returns>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        AuthenticationResult<TokenResponse> result = await _identity.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }

    /// <summary>Revokes a refresh token for logout.</summary>
    /// <param name="request">Opaque refresh token.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>204 regardless of whether the token already expired.</returns>
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _identity.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}

/// <summary>Defines validated customer registration input.</summary>
/// <param name="Email">Unique email address.</param>
/// <param name="Password">Password meeting Identity policy.</param>
public sealed record RegisterRequest([EmailAddress] string Email, [MinLength(12)] string Password);

/// <summary>Defines customer login input.</summary>
/// <param name="Email">Registered email address.</param>
/// <param name="Password">Customer password.</param>
public sealed record LoginRequest([EmailAddress] string Email, string Password);

/// <summary>Defines refresh rotation or revocation input.</summary>
/// <param name="RefreshToken">Opaque raw refresh token.</param>
public sealed record RefreshTokenRequest([Required] string RefreshToken);
