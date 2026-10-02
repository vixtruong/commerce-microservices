using System.Data;
using Commerce.BuildingBlocks.Application.Security;
using Identity.Application.Authentication;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Authentication;

/// <summary>Edits permission bundles transactionally and records safe access-change history.</summary>
public sealed class AccessManagement : IAccessManagement
{
    private readonly IdentityDbContext _db;
    /// <summary>Initializes access administration.</summary>
    /// <param name="db">Identity database.</param>
    public AccessManagement(IdentityDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RoleResponse>> RolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).Take(100).ToArrayAsync(cancellationToken);
        Guid[] ids = roles.Select(r => r.Id).ToArray();
        var claims = await _db.RoleClaims.AsNoTracking().Where(c => ids.Contains(c.RoleId) && c.ClaimType == Permissions.ClaimType)
            .ToArrayAsync(cancellationToken);
        return roles.Select(r => new RoleResponse(r.Id, r.Name!, r.Name is "Admin" or "Customer",
            claims.Where(c => c.RoleId == r.Id).Select(c => c.ClaimValue!).Where(Permissions.All.Contains).Distinct().Order().ToArray())).ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> SetPermissionsAsync(Guid id, Guid actorId, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken)
    {
        if (permissions.Any(p => !Permissions.All.Contains(p)) || permissions.Distinct().Count() != permissions.Count) return false;
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retried transaction must reload authority and discard rolled-back claim/audit changes.
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            // Preserve bootstrap access and stop an actor from modifying the bundles granting their own authority.
            if (role is null || role.Name is "Admin" or "Customer" ||
                await _db.UserRoles.AnyAsync(ur => ur.UserId == actorId && ur.RoleId == id, cancellationToken)) return false;
            var old = await _db.RoleClaims.Where(c => c.RoleId == id && c.ClaimType == Permissions.ClaimType).ToArrayAsync(cancellationToken);
            _db.RoleClaims.RemoveRange(old);
            _db.RoleClaims.AddRange(permissions.Select(p => new IdentityRoleClaim<Guid> { RoleId = id, ClaimType = Permissions.ClaimType, ClaimValue = p }));
            _db.AccessChanges.Add(new AccessChange { Id = Guid.NewGuid(), ActorId = actorId, TargetId = id,
                Action = "role.permissions.replace", Before = string.Join(",", old.Select(c => c.ClaimValue).Order()),
                After = string.Join(",", permissions.Order()), CreatedAtUtc = DateTimeOffset.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            // Existing access JWTs expire in 15 minutes; refresh reloads the authoritative permission set.
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<AccessChangeResponse>> AuditAsync(CancellationToken cancellationToken) =>
        await _db.AccessChanges.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).Take(100)
            .Select(a => new AccessChangeResponse(a.Id, a.ActorId, a.TargetId, a.Action, a.Before, a.After, a.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
}
