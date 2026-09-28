using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش‌های خرید و کنترل انبار.
/// تمرکز روی محاسبه‌هایی است که به‌سادگی غلط درمی‌آیند: میانگین وزنی (نه ساده)،
/// ارزش مغایرت، و تفکیک ضایعات از مصرف داخلی.
/// </summary>
[Collection("Database")]
public class PurchaseReportTests
{
    private readonly TestDatabaseFixture _db;

    public PurchaseReportTests(TestDatabaseFixture db) => _db = db;

    private async Task<IPurchaseReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPurchaseReportQuery>();
    }

    private static decimal Dec(ReportTable t, int row, string key) =>
        Convert.ToDecimal(t.Rows[row][key]);

    private async Task<int> SeedSupplierAsync(string name)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supplier = new Supplier { Name = name };
        ctx.Suppliers.Add(supplier);
        await ctx.SaveChangesAsync();
        return supplier.Id;
    }

    private async Task<int> SeedProductAsync(decimal price, decimal costPrice, decimal stockQty = 0)
    {
        var p = await _db.SeedProductAsync(price, costPrice, stockQty);
        return p.Id;
    }

    private async Task SeedReceiptAsync(
        int supplierId, DateTime date,
        IEnumerable<(int productId, decimal qty, decimal unitCost)> lines)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var receipt = new PurchaseReceipt
        {
            SupplierId = supplierId,
            WarehouseId = 1,
            ReceiptNumber = $"P-{Guid.NewGuid():N}"[..12],
            ReceiptDate = date
        };

        foreach (var (productId, qty, unitCost) in lines)
            receipt.Items.Add(new PurchaseReceiptItem
            {
                ProductId = productId, Quantity = qty, UnitCost = unitCost
            });

        ctx.PurchaseReceipts.Add(receipt);
        await ctx.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task PurchaseBySupplier_ComputesWeightedAverage_NotSimpleAverage()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var supplierId = await SeedSupplierAsync("تأمین‌کنندهٔ آزمایشی");
        var product = (await _db.SeedProductAsync(100_000, 0, 0)).Id;

        // دو خرید با قیمت‌های متفاوت برای همان کالا.
        // میانگین ساده می‌شود (۱۰۰٬۰۰۰ + ۲۰۰٬۰۰۰) ÷ ۲ = ۱۵۰٬۰۰۰ — غلط.
        // میانگین وزنی درست: (۱۰×۱۰۰٬۰۰۰ + ۱×۲۰۰٬۰۰۰) ÷ ۱۱ = ۱۰۹٬۰۹۰
        await SeedReceiptAsync(supplierId, DateTime.UtcNow.AddDays(-10), [(product, 10, 100_000)]);
        await SeedReceiptAsync(supplierId, DateTime.UtcNow.AddDays(-5), [(product, 1, 200_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetPurchaseBySupplierAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);
        Assert.Equal(11m, Dec(table, 0, "TotalQty"));
        Assert.Equal(1_200_000m, Dec(table, 0, "TotalCost"));
        Assert.Equal(109_091m, Dec(table, 0, "AvgUnitCost"));   // وزنی، نه ساده
        Assert.Equal(2m, Dec(table, 0, "ReceiptCount"));
    }

    [SkippableFact]
    public async Task PurchaseBySupplier_GroupsMultipleSuppliers()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var first = await SeedSupplierAsync("تأمین‌کنندهٔ الف");
        var second = await SeedSupplierAsync("تأمین‌کنندهٔ ب");
        var p1 = (await _db.SeedProductAsync(100_000, 0, 0)).Id;
        var p2 = (await _db.SeedProductAsync(200_000, 0, 0)).Id;

        await SeedReceiptAsync(first, DateTime.UtcNow.AddDays(-3), [(p1, 5, 50_000)]);
        await SeedReceiptAsync(second, DateTime.UtcNow.AddDays(-2), [(p2, 4, 60_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetPurchaseBySupplierAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        Assert.Equal(2, table.Rows.Count);
        // مرتب‌سازی نزولی بر اساس مبلغ کل
        Assert.True(Dec(table, 0, "TotalCost") >= Dec(table, 1, "TotalCost"));
    }

    [SkippableFact]
    public async Task ActualCost_ShowsDifferenceBetweenCurrentAndWeighted()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var supplierId = await SeedSupplierAsync("تأمین‌کنندهٔ بها");
        var product = (await _db.SeedProductAsync(100_000, 0, 0)).Id;
        await SeedReceiptAsync(supplierId, DateTime.UtcNow.AddDays(-5), [(product, 10, 80_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetActualCostByProductAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);
        Assert.Equal(80_000m, Dec(table, 0, "WeightedAvg"));   // خرید با بهای ۸۰٬۰۰۰
        Assert.Equal(10m, Dec(table, 0, "TotalQty"));
        Assert.Equal(80_000m, Dec(table, 0, "LastUnitCost"));
        // اختلاف بین بهای فعلی کالا و میانگین موزونِ همین بازه
        Assert.Equal(Dec(table, 0, "CurrentCost") - 80_000m, Dec(table, 0, "Difference"));
    }

    [SkippableFact]
    public async Task StockLoss_SeparatesScrapFromInternalIssue()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = (await _db.SeedProductAsync(100_000, 40_000, 0)).Id;

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var scrap = new ScrapRecord
            {
                WarehouseId = 1, RecordNumber = $"S-{Guid.NewGuid():N}"[..12],
                RecordDate = DateTime.UtcNow.AddDays(-3), Reason = "شکستگی"
            };
            scrap.Items.Add(new ScrapRecordItem { ProductId = product, Quantity = 2 });
            ctx.ScrapRecords.Add(scrap);

            var issue = new InternalIssue
            {
                WarehouseId = 1, IssueNumber = $"I-{Guid.NewGuid():N}"[..12],
                IssueDate = DateTime.UtcNow.AddDays(-2), Purpose = "مصرف اداری"
            };
            issue.Items.Add(new InternalIssueItem { ProductId = product, Quantity = 5 });
            ctx.InternalIssues.Add(issue);

            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockLossAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        Assert.Equal(2, table.Rows.Count);   // یکی ضایعات، یکی مصرف داخلی

        var scrapRow = table.Rows.Single(r => r["Kind"]!.ToString() == "ضایعات");
        Assert.Equal(2m, Convert.ToDecimal(scrapRow["TotalQty"]));
        Assert.Equal("شکستگی", scrapRow["TopReason"]);
        Assert.Equal(80_000m, Convert.ToDecimal(scrapRow["LossValue"]));   // ۲ × ۴۰٬۰۰۰

        var issueRow = table.Rows.Single(r => r["Kind"]!.ToString() == "مصرف داخلی");
        Assert.Equal(5m, Convert.ToDecimal(issueRow["TotalQty"]));
        Assert.Equal(200_000m, Convert.ToDecimal(issueRow["LossValue"]));
    }

    [SkippableFact]
    public async Task StockCountVariance_ExcludesOpenCounts_AndZeroDifferences()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = (await _db.SeedProductAsync(100_000, 25_000, 0)).Id;

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // شمارشِ بسته با مغایرت ⇒ باید بیاید
            var closed = new StockCount
            {
                WarehouseId = 1, CountNumber = $"C-{Guid.NewGuid():N}"[..12],
                CountDate = DateTime.UtcNow.AddDays(-5), Status = StockCountStatus.Closed
            };
            closed.Items.Add(new StockCountItem
            {
                ProductId = product, SystemQuantity = 100, CountedQuantity = 95
            });
            ctx.StockCounts.Add(closed);

            // شمارشِ باز ⇒ نباید بیاید (هنوز نهایی نشده)
            var open = new StockCount
            {
                WarehouseId = 1, CountNumber = $"C-{Guid.NewGuid():N}"[..12],
                CountDate = DateTime.UtcNow.AddDays(-2), Status = StockCountStatus.Open
            };
            open.Items.Add(new StockCountItem
            {
                ProductId = product, SystemQuantity = 100, CountedQuantity = 50
            });
            ctx.StockCounts.Add(open);

            // شمارشِ بسته ولی منطبق ⇒ نباید بیاید (نویز)
            var matched = new StockCount
            {
                WarehouseId = 1, CountNumber = $"C-{Guid.NewGuid():N}"[..12],
                CountDate = DateTime.UtcNow.AddDays(-1), Status = StockCountStatus.Closed
            };
            matched.Items.Add(new StockCountItem
            {
                ProductId = product, SystemQuantity = 100, CountedQuantity = 100
            });
            ctx.StockCounts.Add(matched);

            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockCountVarianceAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        // فقط مغایرت واقعیِ شمارش بسته
        var row = Assert.Single(table.Rows);
        Assert.Equal(-5m, Dec(table, 0, "Discrepancy"));      // ۹۵ − ۱۰۰
        Assert.Equal(-125_000m, Dec(table, 0, "Value"));       // ۵ × ۲۵٬۰۰۰ منفی
    }

    [SkippableFact]
    public async Task AllFive_ReturnColumns_WhenEmpty()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetPurchaseBySupplierAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetActualCostByProductAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetStockLossAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetWarehouseTransfersAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetStockCountVarianceAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Columns);
            Assert.Empty(table.Rows);
        }
    }
}

