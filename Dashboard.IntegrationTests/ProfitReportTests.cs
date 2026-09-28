using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Dashboard.Infrastructure.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش سود و زیان. این گزارش مبنای تصمیم مالی است، پس تست‌ها روی
/// «چیزهایی که به‌سادگی خراب می‌شوند» متمرکزند:
/// فاکتور پیش‌نویس/لغوشده، مرز تاریخ، تخفیف و حمل‌ونقل، سطرهای تکراریِ یک کالا.
/// </summary>
[Collection("Database")]
public class ProfitReportTests
{
    private readonly TestDatabaseFixture _db;

    public ProfitReportTests(TestDatabaseFixture db) => _db = db;

    /// <summary>
    /// ساخت فاکتور با سطرهای دلخواه، مستقیم در دیتابیس — چون هدف، آزمودن
    /// خودِ کوئری گزارش است نه مسیر فروش.
    /// </summary>
    private async Task SeedInvoiceAsync(
        DateTime invoiceDate,
        SalesInvoiceStatus status,
        IEnumerable<(int productId, decimal qty, decimal unitPrice, decimal cost)> lines,
        decimal discount = 0m,
        decimal shipping = 0m)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invoice = new SalesInvoice
        {
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            InvoiceDate = invoiceDate,
            Status = status,
            // انبار مرجع ۱ در ResetTestDataAsync دست‌نخورده می‌ماند
            WarehouseId = 1,
            DiscountAmount = discount,
            ShippingAmount = shipping
        };

        foreach (var (productId, qty, unitPrice, cost) in lines)
        {
            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = productId,
                Quantity = qty,
                UnitPrice = unitPrice,
                CostPrice = cost
            });
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal) - discount + shipping;

        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();
    }

    private async Task<IProfitReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IProfitReportQuery>();
    }

    private static decimal Cell(ReportTable t, int row, string column) =>
        Convert.ToDecimal(t.Rows[row][column]);

    /// <summary>
    /// خواندن یک سلول متنی. سلول‌ها از نوع <c>object?</c> هستند، پس cast
    /// مستقیم به string هشدار nullability می‌داد.
    /// </summary>
    private static string Str(IReadOnlyDictionary<string, object?> row, string key) =>
        row.GetValueOrDefault(key)?.ToString() ?? string.Empty;

    /// <summary>انبار دوم برای تست گروه‌بندی — انبار ۱ در Reset دست‌نخورده می‌ماند.</summary>
    private async Task<int> SeedWarehouseAsync()
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var warehouse = new Warehouse { Name = $"انبار گزارش {Guid.NewGuid():N}"[..20] };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();
        return warehouse.Id;
    }

    private async Task<int> SeedCustomerAsync(string name, string phone)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = name, Phone = phone };
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer.Id;
    }

    /// <summary>همان SeedInvoiceAsync، ولی با انبار و مشتری دلخواه.</summary>
    private async Task SeedInvoiceForCustomerAsync(
        DateTime invoiceDate,
        int customerId,
        IEnumerable<(int productId, decimal qty, decimal unitPrice, decimal cost)> lines)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invoice = new SalesInvoice
        {
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            InvoiceDate = invoiceDate,
            Status = SalesInvoiceStatus.Confirmed,
            WarehouseId = 1,
            CustomerId = customerId
        };

        foreach (var (productId, qty, unitPrice, cost) in lines)
        {
            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = productId,
                Quantity = qty,
                UnitPrice = unitPrice,
                CostPrice = cost
            });
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();
    }

    /// <summary>همان SeedInvoiceAsync، ولی در انبار مشخص — برای تست گروه‌بندی انبار.</summary>
    private async Task SeedInvoiceInWarehouseAsync(
        DateTime invoiceDate,
        int warehouseId,
        IEnumerable<(int productId, decimal qty, decimal unitPrice, decimal cost)> lines)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invoice = new SalesInvoice
        {
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            InvoiceDate = invoiceDate,
            Status = SalesInvoiceStatus.Confirmed,
            WarehouseId = warehouseId
        };

        foreach (var (productId, qty, unitPrice, cost) in lines)
        {
            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = productId,
                Quantity = qty,
                UnitPrice = unitPrice,
                CostPrice = cost
            });
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task DraftAndCanceled_Invoices_AreExcluded()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 200_000, costPrice: 0, stockQty: 0);
        var today = new DateTime(2026, 3, 15);

        await SeedInvoiceAsync(today, SalesInvoiceStatus.Confirmed,
            [(product.Id, 10, 200_000, 100_000)]);
        await SeedInvoiceAsync(today, SalesInvoiceStatus.Draft,
            [(product.Id, 10, 200_000, 100_000)]);
        await SeedInvoiceAsync(today, SalesInvoiceStatus.Canceled,
            [(product.Id, 10, 200_000, 100_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByProductAsync(new ReportFilter());

        Assert.Equal(1, table.Rows.Count);
        // اگر پیش‌نویس/لغوشده هم حساب می‌شدند، فروش و سود ۳ برابر می‌شد
        Assert.Equal(10m, Cell(table, 0, "TotalQty"));
        Assert.Equal(1_000_000m, Cell(table, 0, "Profit"));
    }

    [SkippableFact]
    public async Task DuplicateProductLines_AreSummed_NotDuplicated()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var today = new DateTime(2026, 3, 15);

        await SeedInvoiceAsync(today, SalesInvoiceStatus.Confirmed,
            [(product.Id, 2, 100_000, 60_000), (product.Id, 3, 100_000, 60_000)]);
        await SeedInvoiceAsync(today, SalesInvoiceStatus.Confirmed,
            [(product.Id, 5, 100_000, 60_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByProductAsync(new ReportFilter());

        Assert.Equal(1, table.Rows.Count);
        Assert.Equal(10m, Cell(table, 0, "TotalQty"));
        Assert.Equal(1_000_000m, Cell(table, 0, "Revenue"));
        Assert.Equal(600_000m, Cell(table, 0, "Cost"));
        Assert.Equal(400_000m, Cell(table, 0, "Profit"));
    }

    [SkippableFact]
    public async Task DateFilter_TreatsToDateAsInclusive_WholeDay()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);

        await SeedInvoiceAsync(new DateTime(2026, 3, 10), SalesInvoiceStatus.Confirmed,
            [(product.Id, 1, 100_000, 0)]);
        await SeedInvoiceAsync(new DateTime(2026, 3, 15, 23, 59, 59), SalesInvoiceStatus.Confirmed,
            [(product.Id, 1, 100_000, 0)]);
        await SeedInvoiceAsync(new DateTime(2026, 3, 16), SalesInvoiceStatus.Confirmed,
            [(product.Id, 1, 100_000, 0)]);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByProductAsync(new ReportFilter
        {
            FromDate = new DateTime(2026, 3, 10),
            ToDate = new DateTime(2026, 3, 15)
        });

        // روز ۱۵ باید داخل باشد؛ exclusive گرفتنِ مرز بالا اشتباه رایجی است
        Assert.Equal(2m, Cell(table, 0, "TotalQty"));
    }

    [SkippableFact]
    public async Task MonthlyReport_ExcludesShippingFromRevenue_AndKeepsDiscount()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);

        // ۱۰ × ۱۰۰٬۰۰۰ = ۱٬۰۰۰٬۰۰۰ سطر؛ تخفیف ۱۰۰٬۰۰۰ و حمل ۵۰٬۰۰۰
        // ⇒ TotalAmount = ۱٬۰۰۰٬۰۰۰ − ۱۰۰٬۰۰۰ + ۵۰٬۰۰۰ = ۹۵۰٬۰۰۰
        // درآمدِ «کالا» = TotalAmount − حمل = ۹۰۰٬۰۰۰ (تخفیف کسر شده، حمل نه)
        await SeedInvoiceAsync(new DateTime(2026, 3, 15), SalesInvoiceStatus.Confirmed,
            [(product.Id, 10, 100_000, 40_000)], discount: 100_000, shipping: 50_000);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByMonthAsync(new ReportFilter());

        Assert.Equal(900_000m, Cell(table, 0, "Revenue"));
        Assert.Equal(100_000m, Cell(table, 0, "Discount"));
        Assert.Equal(50_000m, Cell(table, 0, "Shipping"));
        Assert.Equal(400_000m, Cell(table, 0, "Cost"));
        Assert.Equal(500_000m, Cell(table, 0, "Profit"));
    }

    [SkippableFact]
    public async Task LossMakingProducts_ReportOnlyNegativeProfit()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var winner = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var loser = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var today = new DateTime(2026, 3, 15);

        await SeedInvoiceAsync(today, SalesInvoiceStatus.Confirmed,
            [(winner.Id, 5, 100_000, 60_000)]);
        await SeedInvoiceAsync(today, SalesInvoiceStatus.Confirmed,
            [(loser.Id, 2, 100_000, 120_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetLossMakingProductsAsync(new ReportFilter());

        Assert.Equal(1, table.Rows.Count);
        Assert.Equal(loser.Name, table.Rows[0]["Name"]);
        Assert.Equal(-40_000m, Cell(table, 0, "Loss"));
    }

    [SkippableFact]
    public async Task CostIsReadFromSnapshot_NotCurrentProductCost()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        await SeedInvoiceAsync(new DateTime(2026, 3, 15), SalesInvoiceStatus.Confirmed,
            [(product.Id, 10, 100_000, 70_000)]);

        // حالا قیمت کالا عوض می‌شود؛ گزارش قبلی نباید تغییر کند
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = ctx.Products.Single(x => x.Id == product.Id);
            // CostPrice فقط از راه میانگین موزون به‌روز می‌شود (setter خصوصی است)
            p.ApplyWeightedAverageCost(previousQuantity: 10, incomingQuantity: 10, incomingUnitCost: 999_000);
            await ctx.SaveChangesAsync();
        }

        var query = await GetQueryAsync();
        var table = await query.GetProfitByProductAsync(new ReportFilter());

        Assert.Equal(700_000m, Cell(table, 0, "Cost"));
    }

    [SkippableFact]
    public async Task WarehouseReport_GroupsByWarehouse_AndShowsMargin()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // دو انبار مرجع: انبار ۱ موجود است؛ برای دومی باید ساخته شود
        var second = await SeedWarehouseAsync();
        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);

        await SeedInvoiceInWarehouseAsync(new DateTime(2026, 4, 1), 1,
            [(product.Id, 5, 100_000, 60_000)]);
        await SeedInvoiceInWarehouseAsync(new DateTime(2026, 4, 2), second,
            [(product.Id, 10, 100_000, 60_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByWarehouseAsync(new ReportFilter());

        Assert.Equal(2, table.Rows.Count);

        // مرتب‌سازی بر اساس درآمد نزولی است ⇒ انبار دوم (۱٬۰۰۰٬۰۰۰) اول می‌آید
        Assert.Equal(1_000_000m, Cell(table, 0, "Revenue"));
        Assert.Equal(400_000m, Cell(table, 0, "Profit"));      // ۱٬۰۰۰٬۰۰۰ − ۶۰۰٬۰۰۰
        Assert.Equal(40m, Cell(table, 0, "Margin"));            // ۴۰٪
        Assert.Equal(500_000m, Cell(table, 1, "Revenue"));

        // هیچ انباری نباید «نامشخص» باشد — یعنی join نام درست کار می‌کند
        var names = table.Rows
            .Select(r => Str(r, "Warehouse"))
            .ToList();
        Assert.DoesNotContain("نامشخص", names);

        // ستون Margin باید در خروجی هم باشد (هماهنگی ستون با جدول)
        Assert.Contains(table.Columns, c => c.Key == "Margin");
    }

    [SkippableFact]
    public async Task CustomerReport_GroupsByCustomer_AndCountsDistinctInvoices()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
        var customerId = await SeedCustomerAsync("مشتری آزمایشی گزارش", "09120000000");

        // دو فاکتور جداگانه برای یک مشتری ⇒ تعداد فاکتور باید ۲ باشد، نه ۲ سطر
        await SeedInvoiceForCustomerAsync(new DateTime(2026, 5, 1), customerId,
            [(product.Id, 1, 100_000, 70_000)]);
        await SeedInvoiceForCustomerAsync(new DateTime(2026, 5, 2), customerId,
            [(product.Id, 1, 100_000, 70_000)]);

        var query = await GetQueryAsync();
        var table = await query.GetProfitByCustomerAsync(new ReportFilter());

        var row = Assert.Single(table.Rows);
        Assert.Equal("مشتری آزمایشی گزارش", Str(row, "Name"));
        Assert.Equal("09120000000", Str(row, "Phone"));
        Assert.Equal(2m, Cell(table, 0, "InvoiceCount"));   // شمارش فاکتور یکتا
        Assert.Equal(200_000m, Cell(table, 0, "Revenue"));
        Assert.Equal(60_000m, Cell(table, 0, "Profit"));
        Assert.Equal(30m, Cell(table, 0, "Margin"));          // ۳۰٪
    }

    [SkippableFact]
    public async Task EmptyResult_ReturnsValidTable_WithColumnsAndNoRows()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // بازه‌ای که هیچ فاکتوری ندارد
        var filter = new ReportFilter
        {
            FromDate = new DateTime(1990, 1, 1),
            ToDate = new DateTime(1990, 12, 31)
        };

        var query = await GetQueryAsync();

        foreach (var table in new[]
                 {
                     await query.GetProfitByProductAsync(filter),
                     await query.GetLossMakingProductsAsync(filter),
                     await query.GetProfitByWarehouseAsync(filter),
                     await query.GetProfitByCustomerAsync(filter),
                     await query.GetProfitByMonthAsync(filter)
                 })
        {
            Assert.NotEmpty(table.Columns);   // ستون‌ها حتی بدون داده هم باید بیایند
            Assert.Empty(table.Rows);
        }
    }

    /// <summary>
    /// نگهبانِ الزام اصلی: خروجی اکسل باید «همهٔ» رکوردهای فیلترشده باشد،
    /// نه فقط صفحه‌ای که کاربر روی صفحه می‌بیند. اگر کسی دوباره هر دو مسیر را
    /// یکی کند، این تست می‌افتد.
    /// </summary>
    [SkippableFact]
    public async Task ExportPath_ReturnsAllRows_EvenWhenDisplayIsPaged()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // ۷ کالای متفاوت ⇒ ۷ گروه در گزارش سود بر اساس کالا
        var productIds = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            var p = await _db.SeedProductAsync(price: 10_000 * (i + 1), costPrice: 0, stockQty: 0);
            productIds.Add(p.Id);
        }

        // هر کالا در یک فاکتور جداگانه تا هر گروه دقیقاً یک سطر داشته باشد
        for (var i = 0; i < productIds.Count; i++)
        {
            await SeedInvoiceAsync(new DateTime(2026, 6, 1), SalesInvoiceStatus.Confirmed,
                [(productIds[i], 1, 10_000 * (i + 1), 1_000)]);
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        // نمایش: صفحه‌ای با فقط ۲ ردیف
        var display = await query.GetProfitByProductAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.Equal(2, display.Rows.Count);
        Assert.Equal(7, display.TotalCount);      // مجموع واقعی، نه ۲
        Assert.True(display.Truncated);          // یعنی رکورد بیشتری بیرون این صفحه هست

        // خروجی: همان فیلتر، بدون سقف ⇒ باید هر ۷ ردیف باشد
        var export = await query.GetProfitByProductAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(7, export.Rows.Count);
        Assert.Equal(7, export.TotalCount);
        Assert.False(export.Truncated);
    }

    /// <summary>
    /// صفحه‌بندی نباید رکوردها را گم کند: با جمع شدن همهٔ صفحه‌ها باید دقیقاً
    /// همان مجموعِ بدون صفحه‌بندی به دست بیاید (نه کمتر، نه تکراری).
    /// </summary>
    [SkippableFact]
    public async Task Paging_AcrossAllPages_ReturnsEveryRowExactlyOnce()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var productIds = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            var p = await _db.SeedProductAsync(price: 10_000 * (i + 1), costPrice: 0, stockQty: 0);
            productIds.Add(p.Id);
        }
        for (var i = 0; i < productIds.Count; i++)
        {
            await SeedInvoiceAsync(new DateTime(2026, 6, 2), SalesInvoiceStatus.Confirmed,
                [(productIds[i], 1, 10_000 * (i + 1), 1_000)]);
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        var all = await query.GetProfitByProductAsync(filter, ReportQueryOptions.ForExport());
        var expectedSkus = all.Rows.Select(r => Str(r, "Sku")).OrderBy(s => s).ToList();

        var seen = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await query.GetProfitByProductAsync(filter, ReportQueryOptions.ForPage(page, 2));
            seen.AddRange(result.Rows.Select(r => Str(r, "Sku")));
        }

        Assert.Equal(expectedSkus, seen.OrderBy(s => s).ToList());
        Assert.Equal(seen.Count, seen.Distinct().Count());   // هیچ تکراری
    }

    /// <summary>
    /// گزارش کالاهای زیان‌ده: فیلتر «زیان» باید روی کل نتیجه اعمال شود، نه روی
    /// صفحهٔ نمایش — وگرنه خروجی اکسل ردیف سوددهنده هم می‌گیرد و صفحه‌های بعدی
    /// خالی می‌شوند.
    /// </summary>
    [SkippableFact]
    public async Task LossMaking_ExportsOnlyLossRows_EvenWhenDisplayIsPaged()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // ۳ کالای سودده + ۲ کالای زیان‌ده
        for (var i = 0; i < 3; i++)
        {
            var p = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
            await SeedInvoiceAsync(new DateTime(2026, 7, 1), SalesInvoiceStatus.Confirmed,
                [(p.Id, 1, 100_000, 10_000)]);   // سودده
        }

        for (var i = 0; i < 2; i++)
        {
            var p = await _db.SeedProductAsync(price: 100_000, costPrice: 0, stockQty: 0);
            await SeedInvoiceAsync(new DateTime(2026, 7, 2), SalesInvoiceStatus.Confirmed,
                [(p.Id, 1, 10_000, 90_000)]);   // زیان‌ده
        }

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        // نمایش صفحهٔ اول با ۲ ردیف — هر دو باید زیان‌ده باشند، نه سودده
        var display = await query.GetLossMakingProductsAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.Equal(2, display.Rows.Count);
        Assert.Equal(2, display.TotalCount);    // مجموع = تعداد کالاهای زیان‌ده
        Assert.All(display.Rows, r => Assert.True(Convert.ToDecimal(r["Loss"]) < 0));

        // خروجی: فقط زیان‌ده‌ها، هر دو، بدون سودده
        var export = await query.GetLossMakingProductsAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(2, export.Rows.Count);
        Assert.All(export.Rows, r => Assert.True(Convert.ToDecimal(r["Loss"]) < 0));

        // هیچ کالای «سودده» نباید به اکسلِ زیان‌ده نشت کند. کالاهای زیان‌ده خودشان
        // در هر دو گزارش هستند (و باید باشند)، پس اشتراک باید دقیقاً برابرِ
        // مجموعهٔ زیان‌ده باشد، نه خالی.
        var allProducts = (await query.GetProfitByProductAsync(filter, ReportQueryOptions.ForExport()))
            .Rows.Select(r => Str(r, "Sku")).ToHashSet();
        var lossSkus = export.Rows.Select(r => Str(r, "Sku")).ToHashSet();

        Assert.Equal(2, lossSkus.Count);
        Assert.True(lossSkus.IsSubsetOf(allProducts));
        Assert.Equal(lossSkus.Count, allProducts.Intersect(lossSkus).Count());
    }
}

