using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش‌های عملیاتی فروش و انبار: برگشت از فروش، اقساط و گردش موجودی.
/// تمرکز روی چیزهایی است که به‌سادگی عددِ غلط تولید می‌کنند:
/// برگشتِ بدون فاکتورِ ارجاع، قسطِ پرداخت‌شده، و تفکیک ورودی/خروجی موجودی.
/// </summary>
[Collection("Database")]
public class SalesOperationsReportTests
{
    private readonly TestDatabaseFixture _db;

    public SalesOperationsReportTests(TestDatabaseFixture db) => _db = db;

    private async Task<ISalesOperationsReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ISalesOperationsReportQuery>();
    }

    private static string Str(IReadOnlyDictionary<string, object?> row, string key) =>
        row.GetValueOrDefault(key)?.ToString() ?? string.Empty;

    private static decimal Dec(ReportTable t, int row, string key) =>
        Convert.ToDecimal(t.Rows[row][key]);

    private async Task<int> SeedCustomerAsync(string name, string phone = "09120000000")
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = name, Phone = phone };
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer.Id;
    }

    private async Task<int> SeedWarehouseAsync()
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var warehouse = new Warehouse { Name = $"انبار گزارش {Guid.NewGuid():N}"[..20] };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();
        return warehouse.Id;
    }

    /// <summary>فاکتور تأییدشده با یک سطر کالا و برگشتِ متصل به همان فاکتور.</summary>
    private async Task SeedInvoiceAndReturnAsync(
        int productId, decimal qty, decimal unitPrice, decimal cost, decimal returnQty)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invoice = new SalesInvoice
        {
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            InvoiceDate = DateTime.UtcNow.AddDays(-10),
            Status = SalesInvoiceStatus.Confirmed,
            WarehouseId = 1
        };
        invoice.Items.Add(new SalesInvoiceItem
        {
            ProductId = productId, Quantity = qty, UnitPrice = unitPrice, CostPrice = cost
        });
        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var ret = new SalesReturn
        {
            ReturnNumber = $"R-{Guid.NewGuid():N}"[..12],
            ReturnDate = DateTime.UtcNow.AddDays(-5),
            WarehouseId = 1,
            SalesInvoiceId = invoice.Id
        };
        ret.Items.Add(new SalesReturnItem { ProductId = productId, Quantity = returnQty });
        ctx.SalesReturns.Add(ret);
        await ctx.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task SalesReturn_UsesInvoiceLinePrice_AndComputesNetEffect()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 500_000, costPrice: 0, stockQty: 0);
        // ۲ عدد با قیمت واحد ۵۰۰٬۰۰۰ و بهای ۳۰۰٬۰۰۰ برگشت داده می‌شود
        await SeedInvoiceAndReturnAsync(product.Id, 5, 500_000, 300_000, 2);

        var query = await GetQueryAsync();
        var table = await query.GetSalesReturnsAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);
        Assert.Equal(1_000_000m, Dec(table, 0, "Refund"));    // ۲ × ۵۰۰٬۰۰۰
        Assert.Equal(600_000m, Dec(table, 0, "Cost"));       // ۲ × ۳۰۰٬۰۰۰
        Assert.Equal(400_000m, Dec(table, 0, "NetEffect"));   // سودی که از دست رفته
        Assert.Equal(2m, Dec(table, 0, "TotalQty"));
        Assert.Equal(1m, Dec(table, 0, "ReturnCount"));
    }

    [SkippableFact]
    public async Task SalesReturn_GroupsSameProductAcrossMultipleReturns()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 200_000, costPrice: 0, stockQty: 0);
        await SeedInvoiceAndReturnAsync(product.Id, 5, 200_000, 100_000, 1);
        await SeedInvoiceAndReturnAsync(product.Id, 5, 200_000, 100_000, 2);

        var query = await GetQueryAsync();
        var table = await query.GetSalesReturnsAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        // دو برگشتِ یک کالا ⇒ یک ردیف با جمع ۳ عدد
        var row = Assert.Single(table.Rows);
        Assert.Equal(3m, Dec(table, 0, "TotalQty"));
        Assert.Equal(600_000m, Dec(table, 0, "Refund"));
        Assert.Equal(2m, Dec(table, 0, "ReturnCount"));   // دو سندِ برگشت
    }

    [SkippableFact]
    public async Task PagingAndExport_SeparateCorrectly_ForReturns()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // ۵ کالای متفاوت، هرکدام یک برگشت ⇒ ۵ گروه
        for (var i = 0; i < 5; i++)
        {
            var p = await _db.SeedProductAsync(price: 100_000 * (i + 1), costPrice: 0, stockQty: 0);
            await SeedInvoiceAndReturnAsync(p.Id, 5, 100_000 * (i + 1), 50_000, 1);
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        var display = await query.GetSalesReturnsAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.Equal(2, display.Rows.Count);
        Assert.Equal(5, display.TotalCount);
        Assert.True(display.Truncated);

        var export = await query.GetSalesReturnsAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(5, export.Rows.Count);
        Assert.False(export.Truncated);
    }

    /// <summary>فاکتورِ مشتری‌دار برای تست اقساط می‌سازد و شناسه‌اش را برمی‌گرداند.</summary>
    private async Task<int> SeedInvoiceForCustomerAsync(int productId, int customerId)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = new SalesInvoice
        {
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            InvoiceDate = DateTime.UtcNow.AddDays(-60),
            Status = SalesInvoiceStatus.Confirmed,
            WarehouseId = 1,
            CustomerId = customerId
        };
        invoice.Items.Add(new SalesInvoiceItem
        {
            ProductId = productId, Quantity = 1, UnitPrice = 1_000_000, CostPrice = 500_000
        });
        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();
        return invoice.Id;
    }

    [SkippableFact]
    public async Task Installments_ExcludeFullyPaid_AndShowOnlyRemaining()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 1_000_000, costPrice: 0, stockQty: 0);
        var customerId = await SeedCustomerAsync("مشتری اقساطی");
        var invoiceId = await SeedInvoiceForCustomerAsync(product.Id, customerId);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = new InstallmentPlan { SalesInvoiceId = invoiceId };

            // قسط اول: کاملاً پرداخت‌شده ⇒ نباید بیاید
            plan.Installments.Add(new Installment
            {
                SequenceNumber = 1,
                DueDate = DateTime.UtcNow.AddDays(-30),
                Amount = 400_000, PaidAmount = 400_000
            });
            // قسط دوم: نیمه‌پرداخت ⇒ فقط مانده می‌آید
            plan.Installments.Add(new Installment
            {
                SequenceNumber = 2,
                DueDate = DateTime.UtcNow.AddDays(10),
                Amount = 400_000, PaidAmount = 100_000
            });
            ctx.InstallmentPlans.Add(plan);
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetInstallmentsDueAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);      // فقط قسط دوم
        Assert.Equal("مشتری اقساطی", Str(row, "Customer"));
        Assert.Equal(2m, Convert.ToDecimal(row["Sequence"]));
        Assert.Equal(300_000m, Dec(table, 0, "Remaining"));   // ۴۰۰٬۰۰۰ − ۱۰۰٬۰۰۰
        // سررسیدِ ۱۰ روز بعد ⇒ تأخیر صفر
        Assert.Equal(0m, Dec(table, 0, "OverdueDays"));
    }

    [SkippableFact]
    public async Task Installments_ReportOverdueDays_ForPastDue()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 1_000_000, costPrice: 0, stockQty: 0);
        var customerId = await SeedCustomerAsync("بدهکار تاخیری");
        var invoiceId = await SeedInvoiceForCustomerAsync(product.Id, customerId);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = new InstallmentPlan { SalesInvoiceId = invoiceId };
            plan.Installments.Add(new Installment
            {
                SequenceNumber = 1,
                DueDate = DateTime.UtcNow.AddDays(-20),   // ۲۰ روز گذشته
                Amount = 500_000
            });
            ctx.InstallmentPlans.Add(plan);
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetInstallmentsDueAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var overdue = Convert.ToInt32(table.Rows[0]["OverdueDays"]);
        Assert.InRange(overdue, 19, 21);   // حدود ۲۰ روز، با احتساب مرز روز
    }

    [SkippableFact]
    public async Task StockMovement_SeparatesInAndOut_AndComputesNet()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // ۱۰ عدد ورودی، ۴ عدد خروجی
            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = 1,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = 10, UnitCost = 50_000,
                OccurredAt = DateTime.UtcNow.AddDays(-5)
            });
            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = 1,
                Type = StockTransactionType.Sale,
                QuantityChange = -4, UnitCost = 50_000,
                OccurredAt = DateTime.UtcNow.AddDays(-2)
            });
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockMovementAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);
        Assert.Equal(10m, Dec(table, 0, "InQty"));
        Assert.Equal(4m, Dec(table, 0, "OutQty"));
        Assert.Equal(6m, Dec(table, 0, "NetChange"));      // ۱۰ − ۴
        Assert.Equal(300_000m, Dec(table, 0, "Value"));    // ۶ × ۵۰٬۰۰۰
    }

    [SkippableFact]
    public async Task StockMovement_SeparatesByWarehouse()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var secondWarehouse = await SeedWarehouseAsync();

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = 1,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = 5, UnitCost = 20_000,
                OccurredAt = DateTime.UtcNow.AddDays(-3)
            });
            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = secondWarehouse,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = 8, UnitCost = 22_000,
                OccurredAt = DateTime.UtcNow.AddDays(-1)
            });
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetStockMovementAsync(new ReportFilter(), ReportQueryOptions.ForExport());

        Assert.Equal(2, table.Rows.Count);   // یک ردیف برای هر انبار
        Assert.Equal(13m, table.Rows.Sum(r => Convert.ToDecimal(r["InQty"])));
        Assert.Equal(2, table.Rows.Select(r => Str(r, "Warehouse")).Distinct().Count());
        Assert.DoesNotContain("نامشخص", table.Rows.Select(r => Str(r, "Warehouse")));
    }

    /// <summary>
    /// نگهبانِ یک دام واقعی در <c>ReportTableBuilder.Build</c>: نگاشت روی نامِ
    /// property انجام می‌شود، پس اگر نام property با کلیدِ ستون فرق کند، سلول
    /// بی‌سروصدا <c>null</c> می‌شود و هیچ خطایی هم داده نمی‌شود. این اتفاق در
    /// گزارش اقساط افتاد (ستون <c>Customer</c> ولی property <c>CustomerName</c>).
    /// </summary>
    [SkippableFact]
    public async Task EveryColumn_MustHaveAValue_InEveryReport()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 500_000, costPrice: 0, stockQty: 0);
        var customerId = await SeedCustomerAsync("مشتری بررسی ستون", "09121112233");
        var invoiceId = await SeedInvoiceForCustomerAsync(product.Id, customerId);
        await SeedInvoiceAndReturnAsync(product.Id, 5, 500_000, 300_000, 1);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = new InstallmentPlan { SalesInvoiceId = invoiceId };
            plan.Installments.Add(new Installment
            {
                SequenceNumber = 1, DueDate = DateTime.UtcNow.AddDays(5), Amount = 300_000
            });
            ctx.InstallmentPlans.Add(plan);

            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = 1,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = 3, UnitCost = 250_000, OccurredAt = DateTime.UtcNow.AddDays(-1)
            });
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetSalesReturnsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetInstallmentsDueAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetStockMovementAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Rows);
            foreach (var row in table.Rows)
            {
                foreach (var col in table.Columns)
                {
                    Assert.True(row.TryGetValue(col.Key, out _),
                        $"کلید ستون «{col.Title}» در ردیف وجود ندارد — احتمالاً نام property با ستون فرق دارد.");
                }
            }
        }
    }

    [SkippableFact]
    public async Task AllThree_RespectDateFilter_AndEmptyRangeReturnsNothing()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id, WarehouseId = 1,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = 3, UnitCost = 50_000, OccurredAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // بازه‌ای که هیچ داده‌ای ندارد
        var emptyRange = new ReportFilter
        {
            FromDate = new DateTime(1990, 1, 1),
            ToDate = new DateTime(1990, 12, 31)
        };

        var query = await GetQueryAsync();

        Assert.Empty((await query.GetSalesReturnsAsync(emptyRange, ReportQueryOptions.ForExport())).Rows);
        Assert.Empty((await query.GetInstallmentsDueAsync(emptyRange, ReportQueryOptions.ForExport())).Rows);
        Assert.Empty((await query.GetStockMovementAsync(emptyRange, ReportQueryOptions.ForExport())).Rows);

        // ولی دادهٔ امروز بیرون از آن بازه است و با بازهٔ باز پیدا می‌شود
        var all = await query.GetStockMovementAsync(new ReportFilter(), ReportQueryOptions.ForExport());
        Assert.NotEmpty(all.Rows);

        // و بازهٔ امروز هم باید همان را برگرداند
        var todayRange = new ReportFilter { FromDate = DateTime.UtcNow.Date, ToDate = DateTime.UtcNow.Date };
        Assert.NotEmpty((await query.GetStockMovementAsync(todayRange, ReportQueryOptions.ForExport())).Rows);
    }

    [SkippableFact]
    public async Task AllThree_EmptyResult_StillReturnColumns()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetSalesReturnsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetInstallmentsDueAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetStockMovementAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Columns);
            Assert.Empty(table.Rows);
        }
    }
}
