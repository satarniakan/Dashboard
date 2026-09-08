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
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var adminRole = await roleManager.FindByNameAsync(Roles.Admin);
        if (adminRole is not null)
        {
            var existingClaims = await roleManager.GetClaimsAsync(adminRole);
            var existingPermissions = existingClaims.Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).ToList();

            foreach (var permission in Permissions.All)
            {
                if (!existingPermissions.Contains(permission))
                {
                    await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
                }
            }
        }
    }
}