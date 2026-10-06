using Commerce.BuildingBlocks.Application.Queries;

namespace Identity.Application.Authentication;

/// <summary>Contains safe account information without Identity security internals.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Email">Login email.</param>
/// <param name="Roles">Assigned role names.</param>
/// <param name="Permissions">Effective permissions resolved from role bundles.</param>
public sealed record UserResponse(Guid Id, string Email, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);

/// <summary>Defines current-user and authorized administration use cases.</summary>
public interface IUserAdministration
{
    /// <summary>Gets a safe user profile.</summary>
    /// <param name="id">Validated JWT subject.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>User or null.</returns>
    Task<UserResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Gets a bounded user directory.</summary>
    /// <param name="query">Validated page filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe user page.</returns>
    Task<PagedResponse<UserResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    /// <summary>Replaces known roles and revokes existing refresh sessions.</summary>
    /// <param name="id">Target user.</param>
    /// <param name="actorId">Administrator subject, which cannot be the target.</param>
    /// <param name="roles">Allowed existing role names.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>True on success; false for forbidden self-change or invalid input.</returns>
    Task<bool> SetRolesAsync(Guid id, Guid actorId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}
