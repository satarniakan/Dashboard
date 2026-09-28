using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// قفلِ ارسال (claim) در استقرار چندنمونه‌ای: بازپس‌گیری رکوردهای Processing باید
/// «کرش» را از «ارسالِ جاریِ instance دیگر» تشخیص دهد، وگرنه یک پیام دو بار ارسال می‌شود.
/// </summary>
[Collection("Database")]
public class OutboxLeaseTests
{
    private readonly TestDatabaseFixture _db;

    public OutboxLeaseTests(TestDatabaseFixture db) => _db = db;

    private async Task<int> SeedAsync(OutboxStatus status, int attempts = 0, DateTime? processingStartedAt = null)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = new OutboxMessage
        {
            Channel = OutboxChannel.Sms,
            Recipient = $"0912{Guid.NewGuid():N}".Substring(0, 11),
            Body = "کد تست",
            Status = status,
            Attempts = attempts,
            ProcessingStartedAt = processingStartedAt
        };
        ctx.OutboxMessages.Add(message);
        await ctx.SaveChangesAsync();
        return message.Id;
    }

    private async Task<OutboxMessage?> LoadAsync(int id)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await ctx.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    [SkippableFact]
    public async Task Claim_SetsLeaseStart_AndSecondClaimLoses()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var id = await SeedAsync(OutboxStatus.Pending);
        var claimedAt = DateTime.UtcNow.AddMinutes(-3);

        using var scope = _db.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        Assert.True(await repo.TryClaimForSendingAsync(id, maxAttempts: 3, now: claimedAt));
        // instance دوم نباید همان پیام را بردارد
        Assert.False(await repo.TryClaimForSendingAsync(id, maxAttempts: 3, now: DateTime.UtcNow));

        var stored = await LoadAsync(id);
        Assert.Equal(OutboxStatus.Processing, stored!.Status);
        Assert.Equal(1, stored.Attempts);
        // مبنای کهنگی‌سنجی همان لحظهٔ claim است، نه زمان سرور
        Assert.InRange(stored.ProcessingStartedAt!.Value, claimedAt.AddSeconds(-2), claimedAt.AddSeconds(2));
    }

    [SkippableFact]
    public async Task Reclaim_LeavesFreshProcessingRow_Alone()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // دوازده دقیقه پیش claim شده و lease ده دقیقه است ⇒ یتیم حساب می‌شود
        var inFlight = await SeedAsync(OutboxStatus.Processing, attempts: 1,
            processingStartedAt: DateTime.UtcNow.AddSeconds(-30));
        var abandoned = await SeedAsync(OutboxStatus.Processing, attempts: 1,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-12));

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var reclaimed = await repo.ReclaimAbandonedAsync(TimeSpan.FromMinutes(10));
            Assert.Equal(1, reclaimed);
        }

        Assert.Equal(OutboxStatus.Processing, (await LoadAsync(inFlight))!.Status);
        var freed = await LoadAsync(abandoned);
        Assert.Equal(OutboxStatus.Pending, freed!.Status);
        Assert.Null(freed.ProcessingStartedAt);
        // تلاش‌های سوختهٔ instance مرده نباید پاک شود، وگرنه حلقهٔ ارسال بی‌نهایت می‌شود
        Assert.Equal(1, freed.Attempts);
    }

    [SkippableFact]
    public async Task Reclaim_FreesLegacyRows_WithoutLeaseTimestamp()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // پیش از این ستون، رکوردهای Processing بی‌تاریخ ساخته می‌شدند؛ اگر رها شوند
        // تا ابد در Processing می‌مانند و هرگز دوباره ارسال نمی‌شوند.
        var legacy = await SeedAsync(OutboxStatus.Processing, attempts: 1);
        var sent = await SeedAsync(OutboxStatus.Sent, attempts: 1, processingStartedAt: DateTime.UtcNow.AddDays(-3));
        var pending = await SeedAsync(OutboxStatus.Pending);

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            await repo.ReclaimAbandonedAsync(TimeSpan.FromMinutes(10));
        }

        Assert.Equal(OutboxStatus.Pending, (await LoadAsync(legacy))!.Status);
        Assert.Equal(OutboxStatus.Sent, (await LoadAsync(sent))!.Status);
        Assert.Equal(OutboxStatus.Pending, (await LoadAsync(pending))!.Status);
    }
}
