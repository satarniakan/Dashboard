using Dashboard.Application.Services;
using Dashboard.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// نگهبان «آخرین ادمین»: ادمین‌پنل با [Authorize(Roles=Admin)] محافظت می‌شود، پس اگر
/// نقش Admin از تنها ادمین باقی‌مانده برداشته شود، هیچ‌کس نمی‌تواند دوباره وارد شود
/// و این قفل از داخل سیستم باز نمی‌شود.
/// </summary>
[Collection("Database")]
public class AdminRoleSafetyTests
{
    private readonly TestDatabaseFixture _db;

    public AdminRoleSafetyTests(TestDatabaseFixture db) => _db = db;

    // نقش‌ها در دیتابیس تست seed نمی‌شوند (RoleSeeder فقط در استارتاپ واقعی اجرا می‌شود)،
    // و SetRolesAsync از #14 وجود نقش را می‌سنجد؛ پس تست خودش آن‌ها را می‌سازد.
    private async Task EnsureRolesAsync(params string[] roleNames)
    {
        using var scope = _db.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                Assert.True((await roleManager.CreateAsync(new IdentityRole(roleName))).Succeeded);
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string phone)
    {
        using var scope = _db.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phone,
            PhoneNumberConfirmed = true
        };
        var result = await userManager.CreateAsync(user, "TestPass123!");
        Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(e => e.Description)));
        return user;
    }

    // جدول‌های هویتی (AspNetUsers/AspNetUserRoles) در ResetTestDataAsync پاک نمی‌شوند،
    // پس ممکن است از تست‌های قبلی ادمینِ باقی‌مانده داشته باشیم. برای اینکه «تنها ادمین»
    // واقعاً تنها باشد، نقش Admin از همهٔ کاربران دیگر برداشته می‌شود.
    private async Task EnsureSoleAdminAsync(string adminUserId)
    {
        using var scope = _db.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var other in await userManager.GetUsersInRoleAsync(Roles.Admin))
        {
            if (other.Id != adminUserId)
                await userManager.RemoveFromRoleAsync(other, Roles.Admin);
        }
    }

    [SkippableFact]
    public async Task SetRoles_RemovingAdminFromLastAdmin_IsRejected()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();
        await EnsureRolesAsync(Roles.Admin, Roles.Colleague);

        var soleAdmin = await CreateUserAsync($"sole-admin-{Guid.NewGuid():N}@test.local", "09121280001");

        using (var scope = _db.CreateScope())
        {
            var adminService = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

            // ابتدا خودش را ادمین می‌کند (تنها ادمینِ سیستم)
            var grant = await adminService.SetRolesAsync(soleAdmin.Id, new List<string> { Roles.Admin });
            Assert.True(grant.Succeeded, string.Join(" | ", grant.Errors.Select(e => e.Description)));
            await EnsureSoleAdminAsync(soleAdmin.Id);

            // حالا برداشتن Admin از تنها ادمین باید رد شود
            var demote = await adminService.SetRolesAsync(soleAdmin.Id, new List<string> { Roles.Colleague });
            Assert.False(demote.Succeeded, "حذف نقش ادمین از آخرین ادمین باید رد شود.");
        }

        // و نقش‌های کاربر هم دست‌نخورده مانده باشد (نه فقط پیام خطا، بلکه واقعاً ادمین بماند)
        using (var verify = _db.CreateScope())
        {
            var userManager = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = await userManager.GetRolesAsync(soleAdmin);
            Assert.Contains(Roles.Admin, roles);
        }
    }

    [SkippableFact]
    public async Task SetRoles_RemovingAdminWhenAnotherAdminExists_IsAllowed()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();
        await EnsureRolesAsync(Roles.Admin, Roles.Colleague);

        var first = await CreateUserAsync($"admin-one-{Guid.NewGuid():N}@test.local", "09121280002");
        var second = await CreateUserAsync($"admin-two-{Guid.NewGuid():N}@test.local", "09121280003");

        using var scope = _db.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        foreach (var id in new[] { first.Id, second.Id })
        {
            var grant = await adminService.SetRolesAsync(id, new List<string> { Roles.Admin });
            Assert.True(grant.Succeeded, string.Join(" | ", grant.Errors.Select(e => e.Description)));
        }

        // با دو ادمین، تنزل‌دادن یکی مجاز است — گارد نباید سدِ جریان عادی شود
        var demote = await adminService.SetRolesAsync(first.Id, new List<string> { Roles.Colleague });
        Assert.True(demote.Succeeded, string.Join(" | ", demote.Errors.Select(e => e.Description)));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.DoesNotContain(Roles.Admin, await userManager.GetRolesAsync(first));
        Assert.Contains(Roles.Admin, await userManager.GetRolesAsync(second));
    }
}
