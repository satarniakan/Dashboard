using Dashboard.Domain.Entities;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش‌های وضعیت موجودی (فاز ۲). تمرکز روی دو چیزی است که به‌سادگی عددِ
/// گمراه‌کننده می‌دهند: موجودیِ رزرو‌شده و مقایسه با نقطهٔ سفارش.
/// </summary>
[Collection("Database")]
public class StockReportTests
{
    private readonly TestDatabaseFixture _db;

    public StockReportTests(TestDatabaseFixture db) => _db = db;

    private async Task<IStockReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IStockReportQuery>();
    }

    private static decimal Dec(ReportTable t, int row, string key) =>
        Convert.ToDecimal(t.Rows[row][key]);

    private async Task<int> SeedWarehouseAsync()
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var warehouse = new Warehouse { Name = $"انبار تست {Guid.NewGuid():N}"[..20] };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();
        return warehouse.Id;
    }

    /// <summary>
    /// کالا با نقطهٔ سفارشِ دلخواه و موجودی اولیه. <c>ReorderPoint</c> setter خصوصی
    /// دارد، پس فقط از راه سازندهٔ کامل قابل تنظیم است.
    /// </summary>
    private async Task<int> SeedProductAsync(
        decimal price, decimal costPrice, decimal stockQty, int reorderPoint, int warehouseId = 1)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product(
            $"SKU-{Guid.NewGuid():N}"[..20], "کالای تست انبار",
            price, costPrice, reorderPoint: reorderPoint);

        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        ctx.StockLevels.Add(new StockLevel
        {
            ProductId = product.Id,
            WarehouseId = warehouseId,
            QuantityOnHand = stockQty,
            LastUpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return product.Id;
    }

    [SkippableFact]
    public async Task StockOnHand_ReportsAvailable_AndStockValue()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        await SeedProductAsync(price: 500_000, costPrice: 300_000, stockQty: 40, reorderPoint: 5);

        var query = await GetQueryAsync();
        var table = await query.GetStockOnHandAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        // کالای کاشی‌شده در فیکسچر هم هست، پس فقط ردیفِ درست را می‌سنجیم
        var row = table.Rows.Single(r => Convert.ToDecimal(r["OnHand"]) == 40m);
        Assert.Equal(0m, Convert.ToDecimal(row["Reserved"]));
        Assert.Equal(40m, Convert.ToDecimal(row["Available"]));
        Assert.Equal(300_000m, Convert.ToDecimal(row["UnitCost"]));
        // ارزش موجودی = قابل‌فروش × بهای واحد
        Assert.Equal(12_000_000m, Convert.ToDecimal(row["StockValue"]));
    }

    [SkippableFact]
    public async Task Available_IsOnHand_MinusReserved_NotOnHandAlone()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var productId = await SeedProductAsync(
            price: 100_000, costPrice: 50_000, stockQty: 30, reorderPoint: 0);

        // ۱۰ عدد رزرو می‌شود (مثلاً سفارش فروشگاه در جریان)
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var level = ctx.StockLevels.Single(x => x.ProductId == productId);
            level.ReservedQuantity = 10;
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockOnHandAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var row = table.Rows.Single(r => Convert.ToDecimal(r["OnHand"]) == 30m);
        Assert.Equal(10m, Convert.ToDecimal(row["Reserved"]));
        Assert.Equal(20m, Convert.ToDecimal(row["Available"]));   // ۳۰ − ۱۰
        // ارزش باید از «قابل‌فروش» حساب شود نه موجودی کل
        Assert.Equal(1_000_000m, Convert.ToDecimal(row["StockValue"]));   // ۲۰ × ۵۰٬۰۰۰
    }

    [SkippableFact]
    public async Task BelowReorderPoint_OnlyIncludesItemsAtOrUnderThreshold()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // موجودی ۲ با نقطهٔ سفارش ۱۰ ⇒ زیر نقطهٔ سفارش
        await SeedProductAsync(price: 100_000, costPrice: 20_000, stockQty: 2, reorderPoint: 10);
        // موجودی ۵۰ با نقطهٔ سفارش ۱۰ ⇒ سالم
        await SeedProductAsync(price: 100_000, costPrice: 20_000, stockQty: 50, reorderPoint: 10);

        var query = await GetQueryAsync();
        var table = await query.GetBelowReorderPointAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        // فقط کالای کم‌موجود
        var row = Assert.Single(table.Rows);
        Assert.Equal(2m, Dec(table, 0, "Available"));
        Assert.Equal(10m, Dec(table, 0, "ReorderPoint"));
        Assert.Equal(8m, Dec(table, 0, "Shortage"));            // ۱۰ − ۲
        Assert.Equal(18m, Dec(table, 0, "SuggestedOrder"));     // تا دو برابر نقطهٔ سفارش
        Assert.Equal(360_000m, Dec(table, 0, "EstimatedCost"));  // ۱۸ × ۲۰٬۰۰۰
    }

    [SkippableFact]
    public async Task BelowReorderPoint_UsesAvailable_NotOnHand()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // موجودی کل ۳۰ ولی ۲۵ عدد رزرو شده ⇒ قابل‌فروش ۵، زیر نقطهٔ سفارش ۱۰.
        // اگر گزارش اشتباهاً موجودی کل را ملاک بگیرد، این کالا را سالم می‌بیند
        // و خریدِ بی‌مورد پیشنهاد می‌دهد.
        var productId = await SeedProductAsync(
            price: 100_000, costPrice: 20_000, stockQty: 30, reorderPoint: 10);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var level = ctx.StockLevels.Single(x => x.ProductId == productId);
            level.ReservedQuantity = 25;
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetBelowReorderPointAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var row = table.Rows.FirstOrDefault(r => Convert.ToDecimal(r["Available"]) == 5m);
        Assert.NotNull(row);   // باید دیده شود
        Assert.Equal(5m, Convert.ToDecimal(row!["Shortage"]));   // ۱۰ − ۵
    }

    [SkippableFact]
    public async Task StockOnHand_IsolatesEachWarehouse()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var second = await SeedWarehouseAsync();
        var productId = await SeedProductAsync(
            price: 100_000, costPrice: 10_000, stockQty: 20, reorderPoint: 5);

        // همان کالا در انبار دوم هم موجودی دارد
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.StockLevels.Add(new StockLevel
            {
                ProductId = productId,
                WarehouseId = second,
                QuantityOnHand = 7,
                LastUpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockOnHandAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var rows = table.Rows.Where(r => Convert.ToDecimal(r["OnHand"]) is 20m or 7m).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r["Warehouse"]?.ToString()).Distinct().Count());
    }

    [SkippableFact]
    public async Task SearchFilter_NarrowsBothReports()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var id = await SeedProductAsync(price: 100_000, costPrice: 10_000, stockQty: 1, reorderPoint: 10);

        string sku;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            sku = ctx.Products.Single(p => p.Id == id).Sku;
        }

        var query = await GetQueryAsync();
        var hit = new ReportFilter { Search = sku };
        var miss = new ReportFilter { Search = "هیچ-چیز-این-مطابقت-ندارد" };

        var onHandHit = await query.GetStockOnHandAsync(hit, ReportQueryOptions.ForExport());
        var onHandMiss = await query.GetStockOnHandAsync(miss, ReportQueryOptions.ForExport());

        Assert.NotEmpty(onHandHit.Rows);
        Assert.Empty(onHandMiss.Rows);

        var belowHit = await query.GetBelowReorderPointAsync(hit, ReportQueryOptions.ForExport());
        var belowMiss = await query.GetBelowReorderPointAsync(miss, ReportQueryOptions.ForExport());
        Assert.NotEmpty(belowHit.Rows);
        Assert.Empty(belowMiss.Rows);
    }

    [SkippableFact]
    public async Task PagingAndExport_SeparateCorrectly()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // همهٔ این‌ها زیر نقطهٔ سفارش‌اند تا گزارشِ دوم هم چند ردیف داشته باشد
        for (var i = 0; i < 4; i++)
            await SeedProductAsync(
                price: 100_000 * (i + 1), costPrice: 10_000, stockQty: 1, reorderPoint: 10);

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        var display = await query.GetBelowReorderPointAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.True(display.Rows.Count <= 2);
        Assert.True(display.TotalCount >= 4);
        Assert.True(display.Truncated);

        var export = await query.GetBelowReorderPointAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(display.TotalCount, export.Rows.Count);
        Assert.False(export.Truncated);
    }

    [SkippableFact]
    public async Task EveryColumn_HasAValue_InBothReports()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        await SeedProductAsync(price: 100_000, costPrice: 10_000, stockQty: 3, reorderPoint: 10);

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetStockOnHandAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetBelowReorderPointAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Rows);
            foreach (var row in table.Rows)
            {
                foreach (var col in table.Columns)
                {
                    Assert.True(row.TryGetValue(col.Key, out _),
                        $"کلید ستون «{col.Title}» در ردیف وجود ندارد — نام property با ستون فرق دارد.");
                }
            }
        }
    }
}
