using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Dashboard.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// هر سند انباری باید روی تراکنش‌های موجودی‌اش ReferenceId بگذارد تا تاریخچهٔ موجودی
/// قابل ردیابی تا سندِ مرجع باشد (حواله، برگشت، ضایعات، انتقال، انبارگردانی، رسید خرید).
/// </summary>
[Collection("Database")]
public class StockReferenceIdTests
{
    private readonly TestDatabaseFixture _db;

    public StockReferenceIdTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task InternalIssue_StockTransactions_PointToIssueId()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 10);

        int issueId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            issueId = await stock.RegisterInternalIssueAsync(new CreateInternalIssueDto
            {
                WarehouseId = 1,
                Purpose = "تست رفرنس",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 2 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var transactions = await context.StockTransactions
            .Where(t => t.ProductId == product.Id && t.ReferenceType == nameof(InternalIssue))
            .ToListAsync();

        Assert.NotEmpty(transactions);
        Assert.All(transactions, t => Assert.Equal(issueId, t.ReferenceId));
    }

    [SkippableFact]
    public async Task StockTransfer_BothDirections_PointToTransferId()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 10);

        int destinationWarehouseId;
        using (var scope = _db.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var warehouse = new Warehouse($"انبار مقصد {Guid.NewGuid():N}"[..20], $"WH-{Guid.NewGuid():N}"[..10]);
            dbContext.Warehouses.Add(warehouse);
            await dbContext.SaveChangesAsync();
            destinationWarehouseId = warehouse.Id;
        }

        int transferId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            transferId = await stock.RegisterStockTransferAsync(new CreateStockTransferDto
            {
                SourceWarehouseId = 1,
                DestinationWarehouseId = destinationWarehouseId,
                Notes = "تست رفرنس انتقال",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 3 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var transactions = await context.StockTransactions
            .Where(t => t.ProductId == product.Id && t.ReferenceType == nameof(StockTransfer))
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(transferId, t.ReferenceId));

        // موجودی مبدأ کم و مقصد زیاد شده است
        var source = await context.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        var destination = await context.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == destinationWarehouseId);
        Assert.Equal(7m, source.QuantityOnHand);
        Assert.Equal(3m, destination.QuantityOnHand);
    }

    [SkippableFact]
    public async Task SalesReturn_AgainstInvoice_PostsReversalJournalEntry()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        int invoiceId;
        int customerId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری برگشت", "09121250001", "تهران");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id,
                WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 4, UnitPrice = 100_000 }
                }
            }, "it-user");

            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        // برگشت ۱ عدد از همان فاکتور (۱۰۰٬۰۰۰ فروش / ۶۰٬۰۰۰ بهای تمام‌شده)
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterSalesReturnAsync(new CreateSalesReturnDto
            {
                WarehouseId = 1,
                SalesInvoiceId = invoiceId,
                CustomerReference = "مرجوعی تست",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // موجودی: ۱۰ − ۴ فروش + ۱ برگشت = ۷
        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(7m, level.QuantityOnHand);

        var salesReturn = await vContext.SalesReturns.SingleAsync(r => r.SalesInvoiceId == invoiceId);

        var lines = await vContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesReturn)
                     && l.JournalEntry.ReferenceId == salesReturn.Id)
            .ToListAsync();

        Assert.NotEmpty(lines);
        // سند باید تراز باشد
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));

        var accountIds = await vContext.Accounts
            .Where(a => a.Code == SystemAccountCodes.SalesRevenue || a.Code == SystemAccountCodes.CostOfGoodsSold || a.Code == SystemAccountCodes.Inventory || a.Code == SystemAccountCodes.AccountsReceivable)
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        // برگشت درآمد: بدهکار درآمد فروش ۱۰۰٬۰۰۰
        Assert.Equal(100_000m, lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.SalesRevenue]).DebitAmount);
        // برگشت موجودی: بدهکار موجودی ۶۰٬۰۰۰
        Assert.Equal(60_000m, lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.Inventory]).DebitAmount);
        // برگشت COGS: بستانکار ۶۰٬۰۰۰
        Assert.Equal(60_000m, lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.CostOfGoodsSold]).CreditAmount);
        // طلب مشتری به اندازهٔ مبلغ فروش برگشتی کم می‌شود: بستانکار ۱۰۰٬۰۰۰
        Assert.Equal(100_000m, lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.AccountsReceivable]).CreditAmount);
    }

    [SkippableFact]
    public async Task SalesReturn_WhenQuantityExceedsInvoiced_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری تست", "09121250002", "تهران");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id,
                WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 1, UnitPrice = 100_000 }
                }
            }, "it-user");

            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        using var scope2 = _db.CreateScope();
        var stock = scope2.ServiceProvider.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => stock.RegisterSalesReturnAsync(
            new CreateSalesReturnDto
            {
                WarehouseId = 1,
                SalesInvoiceId = invoiceId,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 5 } }
            }, "it-user"));
    }
}
