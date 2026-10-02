namespace Identity.Application.Authentication;

/// <summary>Contains an editable role permission bundle.</summary>
/// <param name="Id">Role identifier.</param>
/// <param name="Name">Role display name.</param>
/// <param name="IsSystemRole">Whether editing this bootstrap role is protected.</param>
/// <param name="Permissions">Assigned permissions.</param>
public sealed record RoleResponse(Guid Id, string Name, bool IsSystemRole, IReadOnlyCollection<string> Permissions);

/// <summary>Defines authoritative effective permission resolution.</summary>
public interface IPermissionResolver
{
    /// <summary>Resolves distinct role permissions for a user from Identity storage.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Effective cataloged permission names.</returns>
    Task<IReadOnlyCollection<string>> ResolveAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Resolves effective permissions for a bounded user page with one database query.</summary>
    /// <param name="userIds">Identifiers already bounded by the administrative page size.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Known, distinct, sorted permissions indexed by each requested user.</returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> ResolveManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>Defines permission-bundle administration and a bounded audit query.</summary>
public interface IAccessManagement
{
    /// <summary>Gets established role bundles.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>At most 100 safe role bundles.</returns>
    Task<IReadOnlyCollection<RoleResponse>> RolesAsync(CancellationToken cancellationToken);
    /// <summary>Changes a non-system role outside the actor's own assigned roles.</summary>
    /// <param name="id">Role identifier.</param>
    /// <param name="actorId">Acting administrator.</param>
    /// <param name="permissions">Cataloged permissions.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>True on success; false for a rejected access change.</returns>
    Task<bool> SetPermissionsAsync(Guid id, Guid actorId, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken);
    /// <summary>Gets the most recent safe access-change audit records.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The latest 100 access changes.</returns>
    Task<IReadOnlyCollection<AccessChangeResponse>> AuditAsync(CancellationToken cancellationToken);
}

/// <summary>Projects safe access-change history without exposing a persistence entity.</summary>
/// <param name="Id">Audit identifier.</param>
/// <param name="ActorId">Acting administrator.</param>
/// <param name="TargetId">Changed user or role.</param>
/// <param name="Action">Access change category.</param>
/// <param name="Before">Former role or permission names.</param>
/// <param name="After">Resulting role or permission names.</param>
/// <param name="CreatedAtUtc">UTC time of the recorded change.</param>
public sealed record AccessChangeResponse(Guid Id, Guid ActorId, Guid TargetId, string Action, string Before, string After, DateTimeOffset CreatedAtUtc);

/// <summary>Records an immutable access-control change without credentials.</summary>
public sealed class AccessChange
{
    /// <summary>Gets or sets the audit identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Gets or sets the acting administrator identifier.</summary>
    public Guid ActorId { get; set; }
    /// <summary>Gets or sets the changed user or role identifier.</summary>
    public Guid TargetId { get; set; }
    /// <summary>Gets or sets the action category.</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>Gets or sets the former roles or permissions.</summary>
    public string Before { get; set; } = string.Empty;
    /// <summary>Gets or sets the resulting roles or permissions.</summary>
    public string After { get; set; } = string.Empty;
    /// <summary>Gets or sets the UTC change time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
