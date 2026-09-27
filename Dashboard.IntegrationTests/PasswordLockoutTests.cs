using Dashboard.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// قفل موقت حساب بعد از تلاش‌های ناموفق — AuthService با lockoutOnFailure:true
/// این قفل را می‌خواهد، ولی بدون تنظیم Identity در پروژه، بی‌اثر بود.
/// </summary>
[Collection("Database")]
public class PasswordLockoutTests
{
    private readonly TestDatabaseFixture _db;

    public PasswordLockoutTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task AfterFiveWrongPasswords_AccountIsLockedOut()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var email = $"lock-{Guid.NewGuid():N}@test.local";

        // ساخت کاربر با رمز عبور
        using (var scope = _db.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = "09121270001",
                PhoneNumberConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "TestPass123!");
            Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(e => e.Description)));
        }

        // پنج تلاش ناموفق
        for (var i = 0; i < 5; i++)
        {
            using var scope = _db.CreateScope();
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var user = await signIn.UserManager.FindByEmailAsync(email);
            var login = await signIn.PasswordSignInAsync(user!, "WrongPass999!", isPersistent: false, lockoutOnFailure: true);
            Assert.False(login.Succeeded);
        }

        // حساب باید قفل شده باشد و حتی رمز درست هم کار نکند
        using (var scope = _db.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);

            Assert.NotNull(user);
            Assert.True(await userManager.IsLockedOutAsync(user!),
                "پس از ۵ تلاش ناموفق، حساب باید موقتاً قفل شود.");
        }

        using (var scope = _db.CreateScope())
        {
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var user = await signIn.UserManager.FindByEmailAsync(email);
            var login = await signIn.PasswordSignInAsync(user!, "TestPass123!", isPersistent: false, lockoutOnFailure: true);
            Assert.True(login.IsLockedOut, "رمز درست هم تا پایان قفل باید رد شود.");
        }
    }

    [SkippableFact]
    public async Task LockoutAllowsLoginAfterReset()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var email = $"unlock-{Guid.NewGuid():N}@test.local";

        using (var scope = _db.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = "09121270002",
                PhoneNumberConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "TestPass123!");
            Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(e => e.Description)));
        }

        for (var i = 0; i < 5; i++)
        {
            using var scope = _db.CreateScope();
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var u = await signIn.UserManager.FindByEmailAsync(email);
            await signIn.PasswordSignInAsync(u!, "WrongPass999!", isPersistent: false, lockoutOnFailure: true);
        }

        // آزادسازی دستی (همان کاری که ادمین می‌کند)
        using (var scope = _db.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            await userManager.SetLockoutEndDateAsync(user!, null);
            await userManager.ResetAccessFailedCountAsync(user!);
        }

        using (var verify = _db.CreateScope())
        {
            var signIn = verify.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var user = await signIn.UserManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            // با CheckPasswordAsync تأیید می‌کنیم که پس از آزادسازی، رمز درست پذیرفته می‌شود
            // (SignInAsync موفق به HttpContext نیاز دارد که در این نوع تست DI وجود ندارد)
            Assert.True(await signIn.UserManager.CheckPasswordAsync(user!, "TestPass123!"),
                "بعد از آزادسازی قفل، رمز درست باید پذیرفته شود.");
            Assert.False(await signIn.UserManager.IsLockedOutAsync(user!));
        }
    }
}
