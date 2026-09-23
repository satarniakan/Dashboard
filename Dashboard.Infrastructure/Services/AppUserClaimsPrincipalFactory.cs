using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Dashboard.Domain.Identity;

namespace Dashboard.Infrastructure.Services;

public class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        var fullName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName
            : $"{user.FirstName} {user.LastName}".Trim();

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            identity.AddClaim(new Claim("FullName", fullName));
        }

        return principal;
    }
}