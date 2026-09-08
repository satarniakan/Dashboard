using Microsoft.AspNetCore.Identity;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IPermissionService
{
    Task<List<string>> GetPermissionsForRoleAsync(string roleName);
    Task SetPermissionsForRoleAsync(string roleName, List<string> permissions);
}

public class PermissionService : IPermissionService
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public PermissionService(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<List<string>> GetPermissionsForRoleAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return new List<string>();

        var claims = await _roleManager.GetClaimsAsync(role);
        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToList();
    }

    public async Task SetPermissionsForRoleAsync(string roleName, List<string> permissions)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return;

        var currentClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var claim in currentClaims.Where(c => c.Type == Permissions.ClaimType))
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var permission in permissions)
        {
            await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
        }
    }
}