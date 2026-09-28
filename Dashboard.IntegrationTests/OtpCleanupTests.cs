using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// رکوردهای کد یک‌بارمصرف هیچ‌وقت خوانده نمی‌شوند (کوئری تأیید فقط ExpiresAt آینده را
/// می‌گیرد) ولی با هر درخواست ورود یکی ساخته می‌شوند — بدون پاک‌سازی، جدول بی‌حد رشد می‌کند.
/// </summary>
[Collection("Database")]
public class OtpCleanupTests
{
    private readonly TestDatabaseFixture _db;

    public OtpCleanupTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task PurgeExpired_RemovesOnlyRowsExpiredPastTheGrace_PerNumber()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        // شمارهٔ یکتا ⇒ نتیجه به رکوردهای تست‌های دیگر وابسته نیست
        var phone = $"0912{Guid.NewGuid():N}".Substring(0, 11);
        var now = DateTime.UtcNow;

        using (var scope = _db.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OtpCodes.AddRange(
                new OtpCode { PhoneNumber = phone, Code = "A", ExpiresAt = now.AddDays(-2), IsUsed = true },
                new OtpCode { PhoneNumber = phone, Code = "B", ExpiresAt = now.AddMinutes(-30), IsUsed = false },
                new OtpCode { PhoneNumber = phone, Code = "C", ExpiresAt = now.AddMinutes(5), IsUsed = false });
            await db.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();
            // یک ساعت دست‌خورده: فقط رکورد دو روزه حذف می‌شود
            await otpService.PurgeExpiredAsync(TimeSpan.FromHours(1));
        }

        using (var verify = _db.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var remaining = await db.OtpCodes.Where(o => o.PhoneNumber == phone).Select(o => o.Code).ToListAsync();

            Assert.DoesNotContain("A", remaining);
            Assert.Contains("B", remaining);
            Assert.Contains("C", remaining);
        }
    }
}
