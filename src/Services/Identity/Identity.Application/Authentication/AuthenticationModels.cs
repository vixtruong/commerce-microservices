namespace Identity.Application.Authentication;

/// <summary>Defines authentication failures without exposing sensitive provider details.</summary>
public enum AuthenticationFailure
{
    /// <summary>Registration input is invalid or already used.</summary>
    RegistrationFailed,
    /// <summary>Credentials are invalid.</summary>
    InvalidCredentials,
    /// <summary>The refresh token is invalid, expired, or already rotated.</summary>
    InvalidRefreshToken
}

/// <summary>Represents an authentication operation result.</summary>
/// <typeparam name="TValue">Success value type.</typeparam>
/// <param name="Value">Success value.</param>
/// <param name="Failure">Failure kind.</param>
public sealed record AuthenticationResult<TValue>(TValue? Value, AuthenticationFailure? Failure)
{
    /// <summary>Gets whether the operation succeeded.</summary>
    public bool IsSuccess => Failure is null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">Success value.</param>
    /// <returns>A successful result.</returns>
    public static AuthenticationResult<TValue> Success(TValue value) => new(value, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">Safe failure category.</param>
    /// <returns>A failed result.</returns>
    public static AuthenticationResult<TValue> Fail(AuthenticationFailure failure) => new(default, failure);
}

/// <summary>Represents a short-lived access token and rotating refresh token.</summary>
/// <param name="AccessToken">Signed JWT access token.</param>
/// <param name="AccessTokenExpiresAtUtc">UTC access expiry.</param>
/// <param name="RefreshToken">Opaque refresh token returned only to the client.</param>
/// <param name="RefreshTokenExpiresAtUtc">UTC refresh expiry.</param>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

/// <summary>Defines secure identity use cases backed by ASP.NET Core Identity.</summary>
public interface IIdentityService
{
    /// <summary>Registers a customer using framework password hashing.</summary>
    /// <param name="email">Customer email.</param>
    /// <param name="password">Unlogged raw password passed directly to Identity.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Tokens or a safe registration failure.</returns>
    Task<AuthenticationResult<TokenResponse>> RegisterAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Authenticates credentials and issues rotating tokens.</summary>
    /// <param name="email">Customer email.</param>
    /// <param name="password">Unlogged raw password.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Tokens or a safe credential failure.</returns>
    Task<AuthenticationResult<TokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Rotates a valid refresh token and revokes the old token atomically.</summary>
    /// <param name="refreshToken">Opaque raw refresh token.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>Replacement tokens or a safe failure.</returns>
    Task<AuthenticationResult<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revokes one refresh token.</summary>
    /// <param name="refreshToken">Opaque raw refresh token.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>A task that completes after revocation.</returns>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
