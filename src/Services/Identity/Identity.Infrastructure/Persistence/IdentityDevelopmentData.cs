using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds deterministic local roles and users through ASP.NET Core Identity's secure password hasher.
/// </summary>
public static class IdentityDevelopmentData
{
    /// <summary>Creates local roles, an administrator, and a customer when absent.</summary>
    /// <param name="users">Identity user manager.</param>
    /// <param name="roles">Identity role manager.</param>
    /// <param name="customerPassword">Development customer password supplied through configuration.</param>
    /// <param name="adminPassword">Development administrator password supplied through configuration.</param>
    /// <returns>A task that completes when all development identities exist.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Identity rejects configured seed credentials.</exception>
    public static async Task SeedAsync(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        string customerPassword,
        string adminPassword)
    {
        foreach (string roleName in new[] { "Customer", "Admin" })
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole<Guid>(roleName)), $"create role {roleName}");
            }
        }

        await EnsureUserAsync(
            users, Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "customer@commerce.local", customerPassword, "Customer");
        await EnsureUserAsync(
            users, Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "admin@commerce.local", adminPassword, "Admin");
    }

    /// <summary>Creates one stable development user and assigns its role.</summary>
    /// <param name="users">Identity user manager.</param>
    /// <param name="id">Stable user identifier.</param>
    /// <param name="email">Login email.</param>
    /// <param name="password">Development-only password.</param>
    /// <param name="role">Role to assign.</param>
    /// <returns>A task that completes when the user is ready.</returns>
    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> users,
        Guid id,
        string email,
        string password,
        string role)
    {
        ApplicationUser? user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { Id = id, UserName = email, Email = email, EmailConfirmed = true };
            EnsureSucceeded(await users.CreateAsync(user, password), $"create user {email}");
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await users.AddToRoleAsync(user, role), $"assign role {role}");
        }
    }

    /// <summary>Converts Identity validation failures into a startup error without exposing credentials.</summary>
    /// <param name="result">Identity operation result.</param>
    /// <param name="operation">Safe operation label.</param>
    /// <exception cref="InvalidOperationException">Thrown when the operation failed.</exception>
    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            string details = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Development identity seed failed to {operation}: {details}.");
        }
    }
}
