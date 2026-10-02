using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Commerce.BuildingBlocks.Application.Security;
using Identity.Application.Authentication;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Authentication;

/// <summary>Implements registration, login, JWT issuance, refresh rotation, and revocation.</summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IPermissionResolver _permissions;

    /// <summary>Initializes the Identity service.</summary>
    /// <param name="users">ASP.NET Core Identity user manager.</param>
    /// <param name="dbContext">Identity context.</param>
    /// <param name="configuration">JWT configuration and non-committed signing key.</param>
    /// <param name="permissions">Authoritative effective-permission resolver.</param>
    public IdentityService(UserManager<ApplicationUser> users, IdentityDbContext dbContext, IConfiguration configuration, IPermissionResolver permissions)
    {
        _users = users;
        _dbContext = dbContext;
        _configuration = configuration;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult<TokenResponse>> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.RegistrationFailed);
        }

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email.Trim(), Email = email.Trim() };
        IdentityResult created = await _users.CreateAsync(user, password);
        if (!created.Succeeded) return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.RegistrationFailed);
        await _users.AddToRoleAsync(user, "Customer");
        return AuthenticationResult<TokenResponse>.Success(await IssueAsync(user, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult<TokenResponse>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _users.FindByEmailAsync(email.Trim());
        if (user is null || !await _users.CheckPasswordAsync(user, password))
        {
            return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.InvalidCredentials);
        }

        return AuthenticationResult<TokenResponse>.Success(await IssueAsync(user, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string hash = Hash(refreshToken);
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retry starts with a clean unit of work rather than retaining a rolled-back replacement.
            _dbContext.ChangeTracker.Clear();
            RefreshToken? stored = await _dbContext.RefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
                return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.InvalidRefreshToken);
            ApplicationUser? user = await _users.FindByIdAsync(stored.UserId.ToString("D"));
            if (user is null) return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.InvalidRefreshToken);

            // The conditional update admits one winner even when different browser tabs race the same token.
            // Claim and replacement commit together, inside Npgsql's retrying execution strategy.
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            int claimed = await _dbContext.RefreshTokens
                .Where(token => token.Id == stored.Id && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, now), cancellationToken);
            if (claimed != 1) return AuthenticationResult<TokenResponse>.Fail(AuthenticationFailure.InvalidRefreshToken);
            TokenResponse replacement = await IssueAsync(user, cancellationToken, saveChanges: false);
            await _dbContext.RefreshTokens.Where(token => token.Id == stored.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ReplacedByTokenHash, Hash(replacement.RefreshToken)), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult<TokenResponse>.Success(replacement);
        });
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string hash = Hash(refreshToken);
        RefreshToken? stored = await _dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Issues a short-lived signed access token and stores a hashed refresh token.</summary>
    /// <param name="user">Authenticated Identity user.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <param name="saveChanges">Whether this method owns the save operation.</param>
    /// <returns>Raw client tokens and expiries.</returns>
    private async Task<TokenResponse> IssueAsync(
        ApplicationUser user,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset accessExpiry = now.AddMinutes(15);
        DateTimeOffset refreshExpiry = now.AddDays(7);
        string issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        string audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        string signingKey = _configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        IList<string> roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString("D")),
            new("email", user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"))
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        // Login and refresh both reload role permissions, so stale access claims are never copied forward.
        claims.AddRange((await _permissions.ResolveAsync(user.Id, cancellationToken)).Select(p => new Claim(Permissions.ClaimType, p)));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(issuer, audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime, credentials);
        string rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(rawRefreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiry
        });
        if (saveChanges) await _dbContext.SaveChangesAsync(cancellationToken);
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), accessExpiry, rawRefreshToken, refreshExpiry);
    }

    /// <summary>Hashes an opaque refresh token for storage and lookup.</summary>
    /// <param name="rawToken">Raw client token.</param>
    /// <returns>Uppercase SHA-256 hexadecimal hash.</returns>
    private static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
