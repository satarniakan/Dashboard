using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش‌های تحلیلی فروشگاه (فاز ۴).
/// تمرکز روی دو قاعده است که اگر اشتباه شوند عدد دروغ می‌گوید:
/// سبدِ فعال «رهاشده» نیست، و کالای بدون دسته نباید از گزارش ناپدید شود.
/// </summary>
[Collection("Database")]
public class AnalyticsReportTests
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsReportTests(TestDatabaseFixture db) => _db = db;

    private async Task<IAnalyticsReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAnalyticsReportQuery>();
    }

    private static decimal Dec(ReportTable t, int row, string key) =>
        Convert.ToDecimal(t.Rows[row][key]);

    /// <summary>سبد خرید با وضعیت انقضای مشخص. CartItem قیمت ندارد.</summary>
    private async Task<int> SeedCartAsync(DateTime expiresAt, params (int productId, decimal qty)[] items)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cart = new Cart
        {
            CookieId = $"cookie-{Guid.NewGuid():N}"[..20],
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            ExpiresAt = expiresAt
        };

        foreach (var (productId, qty) in items)
            cart.Items.Add(new CartItem { ProductId = productId, Quantity = qty });

        ctx.Carts.Add(cart);
        await ctx.SaveChangesAsync();
        return cart.Id;
    }

    [SkippableFact]
    public async Task AbandonedCarts_OnlyExpiredOnes_AndValuesUseCurrentPrice()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 250_000, costPrice: 0, stockQty: 0);

        // سبد منقضی‌شده ⇒ باید بیاید
        var expired = await SeedCartAsync(DateTime.UtcNow.AddDays(-1), (product.Id, 2));
        // سبد فعال ⇒ نباید بیاید (هنوز فرصت خرید دارد)
        await SeedCartAsync(DateTime.UtcNow.AddDays(3), (product.Id, 5));
        // سبد منقضی ولی خالی ⇒ نباید بیاید (درآمدی ندارد)
        await SeedCartAsync(DateTime.UtcNow.AddDays(-1));

        var query = await GetQueryAsync();
        var table = await query.GetAbandonedCartsAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        // فقط سبد منقضی و پُر
        var row = Assert.Single(table.Rows);
        Assert.Equal(expired, Convert.ToInt32(row["CartId"]));
        Assert.Equal(1m, Convert.ToDecimal(row["ItemCount"]));
        Assert.Equal(2m, Convert.ToDecimal(row["TotalQty"]));
        // ارزش از قیمت فعلی کالا: ۲ × ۲۵۰٬۰۰۰
        Assert.Equal(500_000m, Dec(table, 0, "CartValue"));
    }

    [SkippableFact]
    public async Task SalesByCategory_KeepsUncategorizedProducts_Visible()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var categorized = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var uncategorized = await _db.SeedProductAsync(price: 300_000, costPrice: 0, stockQty: 0);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = new Category
            {
                Name = "لوازم خانگی",
                Slug = $"luggage-{Guid.NewGuid():N}"[..20]
            };
            ctx.Categories.Add(category);
            await ctx.SaveChangesAsync();

            // CategoryId setter خصوصی است؛ فقط از راه reflection قابل تنظیم است
            var product = ctx.Products.Single(p => p.Id == categorized.Id);
            typeof(Product).GetProperty("CategoryId")!.SetValue(product, category.Id);

            foreach (var (productId, price) in new[]
                     {
                         (categorized.Id, 100_000m),
                         (uncategorized.Id, 300_000m)
                     })
            {
                var invoice = new SalesInvoice
                {
                    InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
                    InvoiceDate = DateTime.UtcNow.AddDays(-2),
                    Status = SalesInvoiceStatus.Confirmed,
                    WarehouseId = 1
                };
                invoice.Items.Add(new SalesInvoiceItem
                {
                    ProductId = productId, Quantity = 1, UnitPrice = price, CostPrice = price * 0.5m
                });
                invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
                ctx.SalesInvoices.Add(invoice);
            }
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetSalesByCategoryAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        // هر دو دسته باید دیده شوند — نبودِ دسته نباید فروش را حذف کند
        var names = table.Rows.Select(r => r["Category"]!.ToString()).ToList();
        Assert.Contains("لوازم خانگی", names);
        Assert.Contains("بدون دسته‌بندی", names);

        // مجموع فروش دسته‌ها باید با فروش کل برابر باشد
        var totalRevenue = table.Rows.Sum(r => Convert.ToDecimal(r["Revenue"]));
        Assert.Equal(400_000m, totalRevenue);
    }

    [SkippableFact]
    public async Task AllFour_ReturnColumns_WhenEmpty()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetAbandonedCartsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetSalesByCategoryAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetSalesByRegionAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetSubsidiaryLedgerAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Columns);
            Assert.Empty(table.Rows);
        }
    }

    [SkippableFact]
    public async Task PagingAndExport_SeparateCorrectly_ForAbandonedCarts()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        for (var i = 0; i < 4; i++)
        {
            var p = (await _db.SeedProductAsync(100_000 * (i + 1), 0, 0)).Id;
            await SeedCartAsync(DateTime.UtcNow.AddDays(-1), (p, 1));
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        var display = await query.GetAbandonedCartsAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.Equal(2, display.Rows.Count);
        Assert.Equal(4, display.TotalCount);
        Assert.True(display.Truncated);

        var export = await query.GetAbandonedCartsAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(4, export.Rows.Count);
        Assert.False(export.Truncated);
    }
}


