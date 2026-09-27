using Dashboard.Application.DTOs;
using Dashboard.Application.Helpers;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// داشبورد: اعدادی که مدیر بر اساس آن تصمیم می‌گیرد. اگر فاکتور پیش‌نویس یا
/// لغوشده در آمار بیاید، تصمیم مالی اشتباه گرفته می‌شود.
/// </summary>
[Collection("Database")]
public class DashboardServiceTests
{
    private readonly TestDatabaseFixture _db;
    public DashboardServiceTests(TestDatabaseFixture db) => _db = db;

    /// <summary>
    /// فاکتور پیش‌نویس نباید در «فروش امروز» شمرده شود — هنوز فروشی رخ نداده.
    /// </summary>
    [SkippableFact]
    public async Task Dashboard_ExcludesDraftInvoicesFromSales()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 50);
        int customerId;
        int confirmedId, draftId;

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("داشبورد تست", "09121295001", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();

            // فاکتور تأییدشده: فروش واقعی
            confirmedId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                    { new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000m } }
            }, "u");
            await sales.ConfirmInvoiceAsync(confirmedId, "u");

            // فاکتور پیش‌نویس: فقط ذخیره شده، نه تأیید
            draftId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                    { new() { ProductId = product.Id, Quantity = 1, UnitPrice = 100_000m } }
            }, "u");
        }

        using var verify = _db.CreateScope();
        var dashboard = verify.ServiceProvider.GetRequiredService<IDashboardService>();
        var data = await dashboard.GetDashboardDataAsync();

        // دیتابیس بین تست‌ها مشترک است، پس «فروش امروز» شامل فاکتور تست‌های دیگر هم
        // هست. به‌جای مقایسهٔ مطلق، بررسی می‌کنیم که فاکتور پیش‌نویسِ همین تست
        // به جمع اضافه نشده باشد: باید برابر مجموع فقط فاکتورهای تأییدشده باشد.
        var ctx2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        // داشبورد حالا «امروز» را بر مبنای روز تهرانی می‌شمارد؛ انتظار تست هم باید همان
        // منطق را داشته باشد. ToTehranDate در SQL قابل ترجمه نیست، پس فاکتورها را درون
        // حافظه فیلتر می‌کنیم (دقیقاً مثل خود داشبورد).
        var tehranToday = PersianDateHelper.ToTehran(DateTime.UtcNow).Date;
        var confirmedInvoices = await ctx2.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == Dashboard.Domain.Enums.SalesInvoiceStatus.Confirmed)
            .ToListAsync();
        var expectedFromConfirmed = confirmedInvoices
            .Where(i => i.InvoiceDate.ToTehranDate() == tehranToday)
            .Sum(i => i.TotalAmount);

        Assert.Equal(expectedFromConfirmed, data.Kpis.TodaySales);
        Assert.DoesNotContain(data.RecentInvoices, i => i.Id == draftId);

        // مبلغ فاکتور پیش‌نویس نباید در جایی از آمده باشد
        var draft = await ctx2.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == draftId);
        if (draft.TotalAmount > 0)
        {
            var activeInvoices = await ctx2.SalesInvoices.AsNoTracking()
                .Where(i => i.Status != Dashboard.Domain.Enums.SalesInvoiceStatus.Canceled)
                .ToListAsync();
            var withDraft = activeInvoices
                .Where(i => i.InvoiceDate.ToTehranDate() == tehranToday)
                .Sum(i => i.TotalAmount);
            Assert.NotEqual(withDraft, data.Kpis.TodaySales);
        }
    }

    /// <summary>فاکتور لغوشده نباید در آمار بیاید.</summary>
    [SkippableFact]
    public async Task Dashboard_ExcludesCanceledInvoices()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 50);
        int invoiceId;

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("داشبورد لغو", "09121295002", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                    { new() { ProductId = product.Id, Quantity = 3, UnitPrice = 100_000m } }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");
            await sales.CancelInvoiceAsync(invoiceId, "u");
        }

        using var verify = _db.CreateScope();
        var dashboard = verify.ServiceProvider.GetRequiredService<IDashboardService>();
        var data = await dashboard.GetDashboardDataAsync();

        Assert.DoesNotContain(data.RecentInvoices, i => i.Id == invoiceId);
    }

    /// <summary>کالای با موجودی زیر نقطهٔ سفارش باید در هشدار کم‌موجودی بیاید.</summary>
    [SkippableFact]
    public async Task Dashboard_LowStock_FlagsItemsBelowReorderPoint()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 2);
        product.UpdateWarehouseDetails(product.Sku, null, product.Unit, 60_000m, null, null, null, null, reorderPoint: 5);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Update(product);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var dashboard = verify.ServiceProvider.GetRequiredService<IDashboardService>();
        var data = await dashboard.GetDashboardDataAsync();

        Assert.Contains(data.LowStockItems, l => l.ProductName == product.Name);
    }

    /// <summary>روند فروش باید دقیقاً ۷ روز برگرداند (حتی روزهای بدون فروش).</summary>
    [SkippableFact]
    public async Task Dashboard_SalesTrend_HasSevenDays()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardService>();
        var data = await dashboard.GetDashboardDataAsync();

        Assert.Equal(7, data.SalesTrend.Count);
    }
}
