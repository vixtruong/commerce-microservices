using System.Data;
using Commerce.BuildingBlocks.Application.Queries;
using Identity.Application.Authentication;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Authentication;

/// <summary>Implements safe Identity queries and transactional role administration.</summary>
public sealed class UserAdministration : IUserAdministration
{
    private readonly IdentityDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionResolver _permissions;
    /// <summary>Initializes Identity administration.</summary>
    /// <param name="db">Identity-owned database.</param>
    /// <param name="users">Framework role manager.</param>
    /// <param name="permissions">Authoritative effective-permission resolver.</param>
    public UserAdministration(IdentityDbContext db, UserManager<ApplicationUser> users, IPermissionResolver permissions)
    { _db = db; _users = users; _permissions = permissions; }

    /// <inheritdoc />
    public async Task<UserResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return null;
        string[] roles = await (from ur in _db.UserRoles join role in _db.Roles on ur.RoleId equals role.Id
            where ur.UserId == id select role.Name!).ToArrayAsync(cancellationToken);
        return new(user.Id, user.Email ?? string.Empty, roles, await _permissions.ResolveAsync(id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<PagedResponse<UserResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        IQueryable<ApplicationUser> users = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) users = users.Where(u => EF.Functions.ILike(u.Email!, $"%{query.Search.Trim()}%"));
        int total = await users.CountAsync(cancellationToken);
        var page = await users.OrderBy(u => u.Email).ThenBy(u => u.Id).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).Select(u => new { u.Id, u.Email }).ToArrayAsync(cancellationToken);
        Guid[] ids = page.Select(u => u.Id).ToArray();
        var roles = await (from ur in _db.UserRoles join role in _db.Roles on ur.RoleId equals role.Id
            where ids.Contains(ur.UserId) select new { ur.UserId, role.Name }).ToArrayAsync(cancellationToken);
        var responses = new List<UserResponse>();
        var permissions = await _permissions.ResolveManyAsync(ids, cancellationToken);
        foreach (var user in page)
            responses.Add(new(user.Id, user.Email ?? string.Empty, roles.Where(r => r.UserId == user.Id).Select(r => r.Name!).ToArray(),
                permissions[user.Id]));
        return new(responses, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<bool> SetRolesAsync(Guid id, Guid actorId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        // Administrators cannot change their own authority; only established roles can be assigned.
        if (id == actorId || roles.Count == 0 || roles.Distinct().Count() != roles.Count) return false;
        int validRoles = await _db.Roles.CountAsync(r => roles.Contains(r.Name!), cancellationToken);
        if (validRoles != roles.Count) return false;
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Identity saves during role changes; retry from persisted state after any transaction rollback.
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            ApplicationUser? user = await _users.FindByIdAsync(id.ToString());
            if (user is null) return false;
            IList<string> existing = await _users.GetRolesAsync(user);
            _db.AccessChanges.Add(new AccessChange { Id = Guid.NewGuid(), ActorId = actorId, TargetId = id,
                Action = "user.roles.replace", Before = string.Join(",", existing.Order()),
                After = string.Join(",", roles.Order()), CreatedAtUtc = DateTimeOffset.UtcNow });
            IdentityResult removed = await _users.RemoveFromRolesAsync(user, existing.Except(roles));
            if (!removed.Succeeded) throw new InvalidOperationException("Identity role removal failed.");
            IdentityResult added = await _users.AddToRolesAsync(user, roles.Except(existing));
            if (!added.Succeeded) throw new InvalidOperationException("Identity role assignment failed.");
            await _db.SaveChangesAsync(cancellationToken);
            await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}
