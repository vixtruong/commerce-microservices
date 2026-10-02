using Commerce.BuildingBlocks.Application.Security;
using Identity.Application.Authentication;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Authentication;

/// <summary>Resolves role permission claims from the authoritative Identity database.</summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IdentityDbContext _db;
    /// <summary>Initializes effective-permission resolution.</summary>
    /// <param name="db">Identity database.</param>
    public PermissionResolver(IdentityDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await ResolveManyAsync([userId], cancellationToken);
        return result[userId];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> ResolveManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        // IdentityRoleClaim is the RolePermission mapping; UserRole remains ASP.NET Identity's own relation.
        var claims = await (from ur in _db.UserRoles join claim in _db.RoleClaims on ur.RoleId equals claim.RoleId
            where userIds.Contains(ur.UserId) && claim.ClaimType == Permissions.ClaimType
            select new { ur.UserId, Permission = claim.ClaimValue! }).Distinct().ToArrayAsync(cancellationToken);
        // Build the whole page from one bounded join instead of issuing a permission query per account.
        var lookup = claims.Where(c => Permissions.All.Contains(c.Permission)).ToLookup(c => c.UserId, c => c.Permission);
        return userIds.Distinct().ToDictionary(id => id, id => (IReadOnlyCollection<string>)lookup[id].Order().ToArray());
    }
}
