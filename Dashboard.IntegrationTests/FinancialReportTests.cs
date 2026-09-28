using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// گزارش‌های مالی و فروشگاه (فاز ۳).
/// تمرکز روی قاعده‌هایی است که اگر اشتباه شوند، عدد را دروغ می‌گویند:
/// تراز دفتر کل، و اینکه سفارش پرداخت‌نشده درآمد نیست.
/// </summary>
[Collection("Database")]
public class FinancialReportTests
{
    private readonly TestDatabaseFixture _db;

    public FinancialReportTests(TestDatabaseFixture db) => _db = db;

    private async Task<IFinancialReportQuery> GetQueryAsync()
    {
        var scope = _db.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IFinancialReportQuery>();
    }

    private static decimal Dec(ReportTable t, int row, string key) =>
        Convert.ToDecimal(t.Rows[row][key]);

    /// <summary>سند حسابداری متوازن می‌سازد: یک بدهکار و یک بستانکار.</summary>
    private async Task SeedJournalAsync(DateTime date, decimal amount, string description = "سند آزمایشی")
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var accounts = ctx.Accounts.ToList();
        var debitAccount = accounts.FirstOrDefault(a => a.Type == AccountType.Asset)
                           ?? accounts.FirstOrDefault(a => a.Type == AccountType.Expense)
                           ?? accounts[0];
        var creditAccount = accounts.FirstOrDefault(a => a.Id != debitAccount.Id) ?? accounts[1 % accounts.Count];

        var entry = new JournalEntry
        {
            EntryNumber = $"J-{Guid.NewGuid():N}"[..12],
            EntryDate = date,
            Description = description
        };
        entry.Lines.Add(new JournalEntryLine
        {
            AccountId = debitAccount.Id, DebitAmount = amount
        });
        entry.Lines.Add(new JournalEntryLine
        {
            AccountId = creditAccount.Id, CreditAmount = amount
        });

        ctx.JournalEntries.Add(entry);
        await ctx.SaveChangesAsync();
    }

    /// <summary>سفارش فروشگاه با وضعیت دلخواه.</summary>
    private async Task<int> SeedOrderAsync(
        OrderStatus status, decimal subtotal, decimal discount = 0m, decimal shipping = 0m,
        string? discountCode = null, string city = "تهران")
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            OrderNumber = $"O-{Guid.NewGuid():N}"[..12],
            UserId = "test-user",
            CustomerName = "مشتری فروشگاه",
            CustomerPhone = "09121110000",
            City = city,
            ShippingMethod = ShippingMethod.Post,
            ShippingCost = shipping,
            Subtotal = subtotal,
            DiscountAmount = discount,
            DiscountCodeText = discountCode,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            PaidAt = status == OrderStatus.PendingPayment ? null : DateTime.UtcNow.AddDays(-1)
        };

        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();
        return order.Id;
    }

    [SkippableFact]
    public async Task TrialBalance_DebitAndCreditMustBeEqual()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        await SeedJournalAsync(DateTime.UtcNow.AddDays(-3), 500_000, "خرید نقدی");

        var query = await GetQueryAsync();
        var table = await query.GetTrialBalanceAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        Assert.NotEmpty(table.Rows);
        // قاعدهٔ بنیادی حسابداری: هر ریالی که بدهکار شده بستانکار شده
        var totalDebit = table.Rows.Sum(r => Convert.ToDecimal(r["TotalDebit"]));
        var totalCredit = table.Rows.Sum(r => Convert.ToDecimal(r["TotalCredit"]));
        Assert.Equal(totalDebit, totalCredit);

        // مانده هر حساب = بدهکار − بستانکار
        foreach (var row in table.Rows)
        {
            var expected = Convert.ToDecimal(row["TotalDebit"]) - Convert.ToDecimal(row["TotalCredit"]);
            Assert.Equal(expected, Convert.ToDecimal(row["Balance"]));
        }
    }

    [SkippableFact]
    public async Task TrialBalance_RespectsDateFilter()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        await SeedJournalAsync(DateTime.UtcNow.AddDays(-100), 999_000, "سند قدیمی");
        var total = await GetQueryAsync();

        // بازهٔ قدیمی فقط سند قدیمی را می‌بیند
        var oldRange = new ReportFilter
        {
            FromDate = DateTime.UtcNow.AddDays(-110).Date,
            ToDate = DateTime.UtcNow.AddDays(-95).Date
        };
        var oldTable = await total.GetTrialBalanceAsync(oldRange, ReportQueryOptions.ForExport());
        Assert.Equal(999_000m, oldTable.Rows.Sum(r => Convert.ToDecimal(r["TotalDebit"])));

        // بازهٔ خالی چیزی نمی‌بیند
        var emptyRange = new ReportFilter
        {
            FromDate = new DateTime(1990, 1, 1),
            ToDate = new DateTime(1990, 12, 31)
        };
        var emptyTable = await total.GetTrialBalanceAsync(emptyRange, ReportQueryOptions.ForExport());
        Assert.Empty(emptyTable.Rows);
    }

    [SkippableFact]
    public async Task StoreRevenue_ExcludesUnpaidAndCanceledOrders()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // پرداخت‌شده ⇒ باید بیاید
        await SeedOrderAsync(OrderStatus.Paid, 1_000_000, discount: 100_000, shipping: 50_000);
        // در انتظار پرداخت ⇒ نباید بیاید
        await SeedOrderAsync(OrderStatus.PendingPayment, 2_000_000);
        // لغوشده ⇒ نباید بیاید
        await SeedOrderAsync(OrderStatus.Canceled, 3_000_000);

        var query = await GetQueryAsync();
        var table = await query.GetStoreRevenueAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        // فقط سفارش پرداخت‌شده
        var row = Assert.Single(table.Rows);
        Assert.Equal(1_000_000m, Dec(table, 0, "Subtotal"));
        Assert.Equal(100_000m, Dec(table, 0, "Discount"));
        Assert.Equal(50_000m, Dec(table, 0, "Shipping"));
        // مبلغ نهایی = جمع − تخفیف + حمل
        Assert.Equal(950_000m, Dec(table, 0, "Total"));
    }

    [SkippableFact]
    public async Task OrderConversion_IncludesCanceledInDenominator()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // ۳ سفارش: یکی پرداخت‌شده، دو تای دیگر نه
        await SeedOrderAsync(OrderStatus.Paid, 1_000_000);
        await SeedOrderAsync(OrderStatus.PendingPayment, 2_000_000);
        await SeedOrderAsync(OrderStatus.Canceled, 3_000_000);

        var query = await GetQueryAsync();
        var table = await query.GetOrderConversionAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        Assert.Equal(3, table.Rows.Sum(r => Convert.ToInt32(r["OrderCount"])));

        // سهم‌ها با یک رقم اعشار گرد می‌شوند، پس جمعشان ممکن است ۹۹٫۹ یا ۱۰۰٫۱
        // شود — این خطای گردکردن است نه خطای محاسبه. مهم این است که نزدیک ۱۰۰ بماند.
        var totalShare = table.Rows.Sum(r => Convert.ToDecimal(r["Share"]));
        Assert.InRange(totalShare, 99.5m, 100.5m);

        // و هر سهم باید دقیقاً یک‌سوم باشد (۳ سفارش)
        foreach (var row in table.Rows)
            Assert.Equal(33.3m, Convert.ToDecimal(row["Share"]));

        // لغوشده باید «در انتظار پرداخت» جدا شود، نه ادغام
        var statuses = table.Rows.Select(r => r["Status"]!.ToString()).ToList();
        Assert.Contains("لغوشده", statuses);
        Assert.Contains("در انتظار پرداخت", statuses);
        Assert.Contains("پرداخت‌شده", statuses);
    }

    [SkippableFact]
    public async Task DiscountPerformance_ExcludesCanceledOrders()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        await SeedOrderAsync(OrderStatus.Paid, 1_000_000, discount: 200_000, discountCode: "OFF20");
        // لغوشده با همان کد ⇒ نباید در آمار بیاید
        await SeedOrderAsync(OrderStatus.Canceled, 5_000_000, discount: 500_000, discountCode: "OFF20");

        var query = await GetQueryAsync();
        var table = await query.GetDiscountPerformanceAsync(
            new ReportFilter(), ReportQueryOptions.ForExport());

        var row = Assert.Single(table.Rows);
        Assert.Equal("OFF20", row["Code"]);
        Assert.Equal(1m, Convert.ToDecimal(row["UsedOrders"]));
        Assert.Equal(200_000m, Convert.ToDecimal(row["TotalDiscount"]));
        // فروش ایجادشده = مبلغ نهایی سفارش پرداخت‌شده (۱٬۰۰۰٬۰۰۰ − ۲۰۰٬۰۰۰)
        Assert.Equal(800_000m, Convert.ToDecimal(row["GeneratedRevenue"]));
    }

    [SkippableFact]
    public async Task AllEight_ReturnColumns_WhenEmpty()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        foreach (var table in new[]
                 {
                     await query.GetTrialBalanceAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetAccountTransactionsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetCustomerReceiptsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetSupplierPaymentsAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetStoreRevenueAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetOrderConversionAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetDiscountPerformanceAsync(filter, ReportQueryOptions.ForExport()),
                     await query.GetAuditLogAsync(filter, ReportQueryOptions.ForExport())
                 })
        {
            Assert.NotEmpty(table.Columns);
            Assert.Empty(table.Rows);
        }
    }

    [SkippableFact]
    public async Task PagingAndExport_SeparateCorrectly_ForStoreRevenue()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        for (var i = 0; i < 5; i++)
            await SeedOrderAsync(OrderStatus.Paid, 100_000 * (i + 1), city: $"شهر{i}");

        var query = await GetQueryAsync();
        var filter = new ReportFilter();

        var display = await query.GetStoreRevenueAsync(filter, ReportQueryOptions.ForPage(1, 2));
        Assert.Equal(2, display.Rows.Count);
        Assert.Equal(5, display.TotalCount);
        Assert.True(display.Truncated);

        var export = await query.GetStoreRevenueAsync(filter, ReportQueryOptions.ForExport());
        Assert.Equal(5, export.Rows.Count);
        Assert.False(export.Truncated);
    }
}
