using Microsoft.AspNetCore.Identity;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IPermissionService
{
    Task<List<string>> GetPermissionsForRoleAsync(string roleName);
    Task<IdentityResult> SetPermissionsForRoleAsync(string roleName, List<string> permissions);
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

    public async Task<IdentityResult> SetPermissionsForRoleAsync(string roleName, List<string> permissions)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
            return IdentityResult.Failed(new IdentityError { Description = "نقش یافت نشد." });

        // مجوزهای ناشناخته (تایپو یا مقدار ساختگی) عملاً هیچ دسترسی‌ای نمی‌دهند چون authorization
        // فقط مقادیر شناخته‌شده را می‌پذیرد — ولی بی‌صذا ذخیره می‌شدند و UI «ذخیره شد» نشان می‌داد.
        var unknown = permissions.Except(Permissions.All).ToList();
        if (unknown.Any())
            return IdentityResult.Failed(new IdentityError { Description = $"این مجوزها ناشناخته‌اند: {string.Join("، ", unknown)}" });

        var currentClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var claim in currentClaims.Where(c => c.Type == Permissions.ClaimType))
        {
            var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
            if (!removeResult.Succeeded) return removeResult;
        }

        foreach (var permission in permissions)
        {
            var addResult = await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
            if (!addResult.Succeeded) return addResult;
        }

        return IdentityResult.Success;
    }
}