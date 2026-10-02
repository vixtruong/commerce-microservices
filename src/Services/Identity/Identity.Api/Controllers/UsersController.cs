using Commerce.BuildingBlocks.Application.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Commerce.BuildingBlocks.Application.Queries;
using Identity.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>Exposes safe profiles and server-authorized user administration through the existing auth route.</summary>
[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserAdministration _users;
    /// <summary>Initializes the profile controller.</summary>
    /// <param name="users">Identity application use cases.</param>
    public UsersController(IUserAdministration users) => _users = users;

    /// <summary>Gets the authenticated account's safe profile.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>User profile or 401.</returns>
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> MeAsync(CancellationToken cancellationToken)
    {
        UserResponse? user = await _users.GetAsync(Subject(), cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    /// <summary>Gets the administrator's bounded user directory.</summary>
    /// <param name="query">Validated filters.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe user page.</returns>
    [HttpGet("users")]
    [Authorize(Policy = Permissions.UserRead)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> ListAsync([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await _users.ListAsync(query, cancellationToken));

    /// <summary>Gets another account's safe effective access details.</summary>
    /// <param name="id">User identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe account or 404.</returns>
    [HttpGet("users/{id:guid}")]
    [Authorize(Policy = Permissions.UserRead)]
    public async Task<ActionResult<UserResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        UserResponse? user = await _users.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Changes another user's roles and revokes their refresh sessions.</summary>
    /// <param name="id">Target user.</param>
    /// <param name="request">Established roles.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>204 or a safe conflict.</returns>
    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Permissions.RoleManage)]
    public async Task<IActionResult> SetRolesAsync(Guid id, SetRolesRequest request, CancellationToken cancellationToken) =>
        await _users.SetRolesAsync(id, Subject(), request.Roles, cancellationToken) ? NoContent() :
            Problem(statusCode: 409, title: "Identity.RoleChangeRejected", detail: "Choose an existing user, assign known roles, and do not edit your own roles.");

    /// <summary>Reads the validated subject claim.</summary>
    /// <returns>The user identifier.</returns>
    /// <exception cref="UnauthorizedAccessException">The subject is invalid.</exception>
    private Guid Subject() => Guid.TryParse(User.FindFirstValue("sub"), out Guid id) ? id : throw new UnauthorizedAccessException();
}

/// <summary>Defines an administrative role replacement.</summary>
/// <param name="Roles">Known role names.</param>
public sealed record SetRolesRequest([Required, MinLength(1)] string[] Roles);
