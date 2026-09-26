using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Dashboard.Domain.Identity;

namespace Dashboard.Infrastructure;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(
                        $"ساخت نقش «{roleName}» ناموفق بود: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var adminRole = await roleManager.FindByNameAsync(Roles.Admin);
        if (adminRole is not null)
        {
            var existingClaims = await roleManager.GetClaimsAsync(adminRole);
            var existingPermissions = existingClaims.Where(c => c.Type == Permissions.ClaimType).ToList();

            foreach (var permission in Permissions.All)
            {
                if (!existingPermissions.Any(c => c.Value == permission))
                {
                    var addResult = await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
                    if (!addResult.Succeeded)
                        throw new InvalidOperationException(
                            $"افزودن مجوز «{permission}» به نقش ادمین ناموفق بود: {string.Join("; ", addResult.Errors.Select(e => e.Description))}");
                }
            }

            // مجوزهای حذف‌شده از کد باید از نقش ادمین هم پاک شوند تا دسترسی قدیمی باقی نماند
            foreach (var staleClaim in existingPermissions.Where(c => !Permissions.All.Contains(c.Value)))
            {
                var removeResult = await roleManager.RemoveClaimAsync(adminRole, staleClaim);
                if (!removeResult.Succeeded)
                    throw new InvalidOperationException(
                        $"حذف مجوز قدیمی «{staleClaim.Value}» از نقش ادمین ناموفق بود: {string.Join("; ", removeResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}