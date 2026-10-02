using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Commerce.BuildingBlocks.Application.Security;
using Identity.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>Exposes server-authorized permission-bundle management.</summary>
[ApiController]
[Authorize(Policy = Permissions.RoleManage)]
[Route("api/auth")]
public sealed class AccessController : ControllerBase
{
    private readonly IAccessManagement _access;
    /// <summary>Initializes access-control administration.</summary>
    /// <param name="access">Identity application port.</param>
    public AccessController(IAccessManagement access) => _access = access;

    /// <summary>Gets the implemented permission catalog.</summary>
    /// <returns>Permission names.</returns>
    [HttpGet("permissions")]
    public ActionResult<IReadOnlyCollection<string>> PermissionsList() => Ok(Permissions.All);

    /// <summary>Gets the established role bundles.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Role bundles.</returns>
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyCollection<RoleResponse>>> RolesAsync(CancellationToken cancellationToken) =>
        Ok(await _access.RolesAsync(cancellationToken));

    /// <summary>Replaces the permission bundle of an editable role.</summary>
    /// <param name="id">Role identifier.</param>
    /// <param name="request">Known permission names.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>204 or a safe conflict.</returns>
    [HttpPut("roles/{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out Guid actor)) return Unauthorized();
        bool accepted = await _access.SetPermissionsAsync(id, actor, request.Permissions, cancellationToken);
        return accepted ? NoContent() : Problem(statusCode: 409, title: "Identity.PermissionChangeRejected",
            detail: "System roles, your own assigned roles, and unknown permissions cannot be changed.");
    }

    /// <summary>Gets a bounded history of access changes.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The latest 100 audit records.</returns>
    [HttpGet("access/audit")]
    public async Task<ActionResult<IReadOnlyCollection<AccessChangeResponse>>> AuditAsync(CancellationToken cancellationToken) =>
        Ok(await _access.AuditAsync(cancellationToken));
}

/// <summary>Defines a role permission replacement.</summary>
/// <param name="Permissions">Known permission names, with an empty bundle permitted.</param>
public sealed record SetPermissionsRequest([Required, MaxLength(100)] string[] Permissions);
