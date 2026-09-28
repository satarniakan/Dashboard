using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// اعلامیه ارزش افزوده: خلاصهٔ دوره (ابتدا/گردش/پایان ۲۳۰۰ و ۱۳۵۰ و خالص قابل پرداخت)
/// باید با اسنادی که VatTests چرخهٔ کاملشان را ادعا می‌کنند یکی بخواند؛ و جدولِ گردش
/// هیچ سلول خالی‌ای نداشته باشد (نگهبانِ سلولِ nullِ ReportTableBuilder).
/// </summary>
[Collection("Database")]
public class VatReportTests
{
    private readonly TestDatabaseFixture _db;

    public VatReportTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task Summary_MatchesPostedDocuments_AndNetIsPayableMinusCredit()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        // فروش با مالیات ۱۰٪: مبنای ۲۰۰٬۰۰۰ + ۸۰٬۰۰۰ حمل → مالیات ۲۸٬۰۰۰ در ۲۳۰۰ بستانکار
        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Customers.Add(new Customer("مشتری اعلامیه", "09121299001", "تهران"));
            await context.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            var invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = context.Customers.First().Id,
                WarehouseId = 1,
                ShippingAmount = 80_000,
                TaxPercent = 10m,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 }
                }
            }, "it-user");
            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        // خرید با اعتبار مالیاتی ۵۰٬۰۰۰ در ۱۳۵۰ بدهکار
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Suppliers.Add(new Supplier("تأمین‌کننده اعلامیه"));
            await context.SaveChangesAsync();

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = context.Suppliers.First().Id,
                WarehouseId = 1,
                TaxAmount = 50_000m,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 50_000 }
                }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vat = verify.ServiceProvider.GetRequiredService<IVatReportQuery>();
        var summary = await vat.GetVatSummaryAsync(new ReportFilter());

        // ۲۳۰۰: بدهکار صفر، بستانکار ۲۸٬۰۰۰
        Assert.Equal(0m, summary.Payable.PeriodDebit);
        Assert.Equal(28_000m, summary.Payable.PeriodCredit);
        Assert.Equal(28_000m, summary.Payable.ClosingCreditBalance);

        // ۱۳۵۰: بدهکار ۵۰٬۰۰۰، بستانکار صفر
        Assert.Equal(50_000m, summary.Receivable.PeriodDebit);
        Assert.Equal(0m, summary.Receivable.PeriodCredit);
        Assert.Equal(50_000m, summary.Receivable.ClosingDebitBalance);

        // خالص: ۲۸٬۰۰۰ − ۵۰٬۰۰۰ = −۲۲٬۰۰۰ (اعتبار مالیاتی مانده، بدهکار نیستیم)
        Assert.Equal(-22_000m, summary.NetVatPayable);

        // بازهٔ فقط-بعد-از-امروز: گردش صفر، یعنی «ابتدای دوره» درست جمع می‌شود
        var future = new ReportFilter { FromDate = DateTime.UtcNow.Date.AddDays(7) };
        var futureSummary = await vat.GetVatSummaryAsync(future);
        Assert.Equal(0m, futureSummary.Payable.PeriodCredit);
        Assert.Equal(0m, futureSummary.Receivable.PeriodDebit);
    }

    [SkippableFact]
    public async Task Lines_EveryColumnHasValue_AndDateFilterWorks()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Customers.Add(new Customer("مشتری گردش اعلامیه", "09121299002", "تهران"));
            context.Suppliers.Add(new Supplier("تأمین‌کننده گردش اعلامیه"));
            await context.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            var invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = context.Customers.First().Id,
                WarehouseId = 1,
                TaxPercent = 10m, // ۲۰٬۰۰۰
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 }
                }
            }, "it-user");
            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = context.Suppliers.First().Id,
                WarehouseId = 1,
                TaxAmount = 10_000m,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 5, UnitCost = 50_000 }
                }
            }, "it-user");
        }

        using var scope2 = _db.CreateScope();
        var vat = scope2.ServiceProvider.GetRequiredService<IVatReportQuery>();

        // بدون فیلتر: دو سند مالیاتی → دو سطر
        var table = await vat.GetVatLinesAsync(new ReportFilter(), ReportQueryOptions.ForExport());
        Assert.Equal(2, table.Rows.Count);

        // هیچ سلول خالی‌ای نباشد — اگر نام property با ReportColumn.Key یکی نباشد
        // بی‌سروصدا null می‌شود و همین ادعا آن را می‌گیرد
        foreach (var row in table.Rows)
            foreach (var col in table.Columns)
                Assert.True(row.GetValueOrDefault(col.Key) is not null,
                    $"ستون {col.Key} سطر {string.Join(',', row.Values)} خالی است.");

        // فیلتر آینده: هیچ سطری
        var futureTable = await vat.GetVatLinesAsync(
            new ReportFilter { FromDate = DateTime.UtcNow.Date.AddDays(7) },
            ReportQueryOptions.ForExport());
        Assert.Empty(futureTable.Rows);

        // جست‌وجو فقط جدول را فیلتر می‌کند — «خرید» باید فقط سطر ۱۳۵۰ را بیاورد
        var searched = await vat.GetVatLinesAsync(
            new ReportFilter { Search = "اعتبار مالیاتی" }, ReportQueryOptions.ForExport());
        Assert.Equal("1350", searched.Rows.Single()["AccountCode"]!.ToString());
    }
}
