using Dashboard.Application.Services;
using Dashboard.Domain.Interfaces;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// دادهٔ نمونهٔ برنامه باید گزارش‌ها را تغذیه کند. بدون این تست، خالی بودنِ
/// یک گزارش در نصبِ تازه به‌جای «کاربر هنوز داده نساخته» با «بذر داده خراب است»
/// اشتباه گرفته می‌شود.
/// </summary>
[Collection("Database")]
public class TestDataSeederTests
{
    private readonly TestDatabaseFixture _db;

    public TestDataSeederTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task Seed_CreatesCategories_AndAssignsEveryProduct()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ITestDataSeederService>();
        await seeder.SeedAsync(null);

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categories = await ctx.Categories.ToListAsync();
        Assert.NotEmpty(categories);

        // گزارش «فروش بر اساس دسته» فقط وقتی مفید است که کالاها دسته داشته باشند.
        // اگر seed دسته نسازد، همه‌چیز در ردیفِ «بدون دسته‌بندی» می‌ریزد.
        var products = await ctx.Products.ToListAsync();
        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.CategoryId.HasValue,
                $"کالای «{p.Name}» بدون دسته ساخته شده — گزارش دسته بی‌معنا می‌شود."));
    }

    [SkippableFact]
    public async Task Seed_CreatesExpiredCarts_SoAbandonedCartReportHasData()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ITestDataSeederService>();
        await seeder.SeedAsync(null);

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expiredCarts = await ctx.Carts
            .Include(c => c.Items)
            .Where(c => c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        Assert.NotEmpty(expiredCarts);
        // سبد خالی در گزارش نمی‌آید، پس دست‌کم باید یک سطر کالا داشته باشد
        Assert.Contains(expiredCarts, c => c.Items.Count > 0);
    }

    [SkippableFact]
    public async Task SeededData_FeedsCategoryAndAbandonedCartReports()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        using (var scope = _db.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<ITestDataSeederService>();
            await seeder.SeedAsync(null);
        }

        // مسیر واقعیِ مصرف‌کنندهٔ داده: گزارش‌ها باید با بذر داده ردیف بدهند
        using (var scope = _db.CreateScope())
        {
            var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsReportQuery>();
            var filter = new ReportFilter();

            var byCategory = await analytics.GetSalesByCategoryAsync(
                filter, ReportQueryOptions.ForExport());
            Assert.NotEmpty(byCategory.Rows);
            // دسته‌های واقعی باید بیایند، نه فقط «بدون دسته‌بندی»
            var names = byCategory.Rows.Select(r => r["Category"]!.ToString()).ToList();
            Assert.DoesNotContain("بدون دسته‌بندی", names);

            var carts = await analytics.GetAbandonedCartsAsync(
                filter, ReportQueryOptions.ForExport());
            Assert.NotEmpty(carts.Rows);
            // ارزش سبد باید مثبت باشد (قیمت کالا خوانده شده)
            Assert.All(carts.Rows, r =>
                Assert.True(Convert.ToDecimal(r["CartValue"]) > 0));
        }
    }

    /// <summary>
    /// نگهبانِ باگِ حذفِ سبدهای منقضی.
    ///
    /// CartRepository قبلاً مستقیم روی جدول Carts حذف می‌کرد، بدون آنکه سطرهای
    /// CartItems را پاک کند ⇒ خطای FK_CartItems_Carts_CartId و از کار افتادنِ
    /// صفحهٔ فروشگاه. این تست عمداً سبدِ منقضیِ «پر» می‌سازد (سناریویی که در
    /// نصبِ واقعی رخ می‌دهد) و بعد یک سبدِ تازه می‌سازد تا ثابت شود پاک‌سازی
    /// هم درست کار می‌کند و هم نمی‌ترکد.
    /// </summary>
    [SkippableFact]
    public async Task AddingToCart_CleansUpExpiredCarts_WithoutForeignKeyError()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ITestDataSeederService>();
        await seeder.SeedAsync(null);

        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // سبدهای «seed-abandoned-*» از قبل منقضی و پر هستند — کافی است
        // اگر خطایی نباشد یعنی پاک‌سازی FK-امن است.
        var result = await cartService.AddToCartAsync("brand-new-cart", 1, 1);

        Assert.True(result.Success, result.Message);

        // سبدهای منقضی باید واقعاً پاک شده باشند (نه فقط بی‌سروصدا رد شده باشند)
        var remaining = await ctx.Carts.ToListAsync();
        Assert.DoesNotContain(remaining, c => c.CookieId.StartsWith("seed-abandoned-"));
    }
}
