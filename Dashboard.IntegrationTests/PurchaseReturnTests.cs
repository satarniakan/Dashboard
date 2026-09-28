using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// برگشت خرید به تأمین‌کننده: کاهش انبار با سقف «دریافتی منهای برگشت‌های قبلی»،
/// سند حسابداری بدهکارِ پرداختنی/بستانکارِ موجودی با «بهای اصلیِ رسید»،
/// و اصلاح میانگین موزون برای ارزشِ خارج‌شده.
/// </summary>
[Collection("Database")]
public class PurchaseReturnTests
{
    private readonly TestDatabaseFixture _db;

    public PurchaseReturnTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task PurchaseReturn_DecreasesStock_DebitsPayable_AtOriginalCost()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 50_000, stockQty: 0);

        int supplierId, receiptId;
        string receiptNumber;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier("تأمین‌کننده برگشت تست");
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            receiptId = await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 50_000 }
                }
            }, "it-user");

            receiptNumber = (await context.PurchaseReceipts.SingleAsync(r => r.Id == receiptId)).ReceiptNumber;
        }

        int returnId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            returnId = await stock.RegisterPurchaseReturnAsync(new CreatePurchaseReturnDto
            {
                PurchaseReceiptId = receiptId,
                WarehouseId = 1,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 4 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // موجودی ۱۰ − ۴ = ۶ و بهای میانگین دست‌نخورده (بهای اصلی = میانگین فعلی)
        var level = await vCtx.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(6m, level.QuantityOnHand);
        var productAfter = await vCtx.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(50_000m, productAfter.CostPrice);

        // تراکنش انبار از نوع برگشت خرید با مقدار منفی
        var tx = await vCtx.StockTransactions.SingleAsync(t =>
            t.ReferenceType == nameof(PurchaseReturn) && t.ReferenceId == returnId);
        Assert.Equal(StockTransactionType.PurchaseReturn, tx.Type);
        Assert.Equal(-4m, tx.QuantityChange);

        // سند حسابداری: بدهکار پرداختنی (۲۱۰۰ با معین تأمین‌کننده) / بستانکار موجودی (۱۳۰۰)
        var lines = await vCtx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(PurchaseReturn)
                     && l.JournalEntry.ReferenceId == returnId)
            .ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));

        var apAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.AccountsPayable).Select(a => a.Id).SingleAsync();
        var invAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.Inventory).Select(a => a.Id).SingleAsync();

        var apLine = lines.Single(l => l.AccountId == apAccountId);
        Assert.Equal(200_000m, apLine.DebitAmount);
        Assert.Equal("Supplier", apLine.SubsidiaryType);
        Assert.Equal(supplierId, apLine.SubsidiaryId);

        var invLine = lines.Single(l => l.AccountId == invAccountId);
        Assert.Equal(200_000m, invLine.CreditAmount);
        Assert.Equal(0m, invLine.DebitAmount);

        // جستجوی صفحهٔ برگشت: ماندهٔ قابل‌برگشت باید ۶ باشد (دریافتی ۱۰ منفی برگشت ۴)
        var lookup = await verify.ServiceProvider.GetRequiredService<IStockService>()
            .GetPurchaseReceiptForReturnAsync(receiptNumber);
        Assert.NotNull(lookup);
        var lookupItem = Assert.Single(lookup!.Items);
        Assert.Equal(4m, lookupItem.AlreadyReturned);
    }

    [SkippableFact]
    public async Task PurchaseReturn_OverRemaining_Rejected_AndStockUnchanged()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 50_000, stockQty: 0);

        int receiptId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Suppliers.Add(new Supplier("تأمین‌کننده سقف برگشت"));
            await context.SaveChangesAsync();

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            receiptId = await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = context.Suppliers.First().Id,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 50_000 }
                }
            }, "it-user");
        }

        using var scope2 = _db.CreateScope();
        var service = scope2.ServiceProvider.GetRequiredService<IStockService>();

        // درخواست ۷ عدد اما کل قابل‌برگشت ۱۰ است — ولی ۱۲ بیشتر از کل، باید رد شود
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.RegisterPurchaseReturnAsync(
            new CreatePurchaseReturnDto
            {
                PurchaseReceiptId = receiptId,
                WarehouseId = 1,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 12 } }
            }, "it-user"));
        Assert.IsType<BusinessRuleException>(ex);

        // هیچ سندی و تراکنشی ثبت نشده باشد
        using var verify = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var level = await verify.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(10m, level.QuantityOnHand);
        Assert.False(await verify.PurchaseReturns.AnyAsync());
        Assert.False(await verify.StockTransactions.AnyAsync(t => t.Type == StockTransactionType.PurchaseReturn));
    }

    [SkippableFact]
    public async Task PurchaseReturn_WithPriceChange_RecomputesWeightedAverage()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 50_000, stockQty: 0);

        int supplierId, receiptAId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier("تأمین‌کننده میانگین");
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var dto = new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 50_000 }
                }
            };
            receiptAId = await stock.RegisterPurchaseReceiptAsync(dto, "it-user");

            // خرید دوم گران‌تر: میانگین = (۱۰×۵۰ + ۱۰×۹۰) ÷ ۲۰ = ۷۰٬۰۰۰
            dto.Items = new List<PurchaseReceiptItemInput>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitCost = 90_000 }
            };
            await stock.RegisterPurchaseReceiptAsync(dto, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            // برگشت کل بچ اول با بهای اصلیِ ۵۰٬۰۰۰ (نه میانگین ۷۰٬۰۰۰)
            await stock.RegisterPurchaseReturnAsync(new CreatePurchaseReturnDto
            {
                PurchaseReceiptId = receiptAId,
                WarehouseId = 1,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 10 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var level = await vCtx.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(10m, level.QuantityOnHand);

        // میانگین جدید = (۲۰×۷۰٬۰۰۰ − ۱۰×۵۰٬۰۰۰) ÷ ۱۰ = ۹۰٬۰۰۰ — دقیقاً بچ باقیمانده
        var productAfter = await vCtx.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(90_000m, productAfter.CostPrice);
    }

    [SkippableFact]
    public async Task PurchaseReturn_ProductNotInReceipt_Rejected()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var productA = await _db.SeedProductAsync(price: 100_000, costPrice: 50_000, stockQty: 0);
        var productB = await _db.SeedProductAsync(price: 200_000, costPrice: 80_000, stockQty: 5);

        int receiptId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Suppliers.Add(new Supplier("تأمین‌کننده کالای غریبه"));
            await context.SaveChangesAsync();

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            receiptId = await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = context.Suppliers.First().Id,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = productA.Id, Quantity = 5, UnitCost = 50_000 }
                }
            }, "it-user");
        }

        using var scope2 = _db.CreateScope();
        var service = scope2.ServiceProvider.GetRequiredService<IStockService>();

        // کالای B در رسید نبوده — باید با پیام کسب‌وکار رد شود، نه خطای فنی
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.RegisterPurchaseReturnAsync(
            new CreatePurchaseReturnDto
            {
                PurchaseReceiptId = receiptId,
                WarehouseId = 1,
                Items = new List<StockItemInput> { new() { ProductId = productB.Id, Quantity = 1 } }
            }, "it-user"));
        Assert.IsType<BusinessRuleException>(ex);
    }
}
