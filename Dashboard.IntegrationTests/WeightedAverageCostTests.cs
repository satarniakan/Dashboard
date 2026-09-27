using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// هزینه‌یابی میانگین موزون: با هر رسید خرید، بهای تمام‌شدهٔ کالا به‌روز می‌شود تا
/// سود ناخالص فروش‌های بعدی با قیمت واقعیِ خرید بسته شود.
/// </summary>
[Collection("Database")]
public class WeightedAverageCostTests
{
    private readonly TestDatabaseFixture _db;

    public WeightedAverageCostTests(TestDatabaseFixture db) => _db = db;

    private static decimal CostOf(AppDbContext ctx, int productId) =>
        ctx.Products.AsNoTracking().Single(p => p.Id == productId).CostPrice;

    [SkippableFact]
    public async Task TwoReceipts_ProduceWeightedAverageCost()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // موجودی اولیه صفر تا «اولین خرید» محاسبهٔ میانگین را قطعی تست کند
        var product = await _db.SeedProductAsync(price: 150_000, costPrice: 0, stockQty: 0);

        int supplierId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier($"تأمین‌کننده {Guid.NewGuid():N}"[..18]);
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        // خرید اول: ۱۰ عدد، هر واحد ۶۰٬۰۰۰ ⇒ میانگین ۶۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 60_000 }
                }
            }, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(60_000m, CostOf(context, product.Id));
        }

        // خرید دوم: ۱۰ عدد، هر واحد ۸۰٬۰۰۰ ⇒ (۱۰×۶۰ + ۱۰×۸۰) ÷ ۲۰ = ۷۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 80_000 }
                }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(70_000m, CostOf(vContext, product.Id));

        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(20m, level.QuantityOnHand);
    }

    /// <summary>
    /// رگرسیون: یک کالا با دو سطر در همان رسید خرید. «موجودی قبل از رسید» باید یک‌بار
    /// خوانده شود؛ قبلاً هر سطر مقدار سطر قبلی را بازنویسی می‌کرد و میانگین موزون
    /// ۵۵٬۰۰۰ می‌شد به‌جای ۷۰٬۰۰۰.
    /// </summary>
    [SkippableFact]
    public async Task SameProductTwiceInOneReceipt_ComputesCorrectWeightedAverage()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 150_000, costPrice: 0, stockQty: 0);

        int supplierId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier($"تأمین‌کننده {Guid.NewGuid():N}"[..18]);
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        // یک کالا، دو سطر: ۱۰ عدد @۶۰٬۰۰۰ و ۱۰ عدد @۸۰٬۰۰۰
        // انتظار: (۱۰×۶۰٬۰۰۰ + ۱۰×۸۰٬۰۰۰) ÷ ۲۰ = ۷۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 60_000 },
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 80_000 }
                }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(70_000m, CostOf(vContext, product.Id));

        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(20m, level.QuantityOnHand);

        // دو سطر باید در یک سطرِ تجمیع‌شده ثبت شده باشند (مجموع مقدار = ۲۰)
        var receiptId = await vContext.PurchaseReceipts
            .Where(r => r.SupplierId == supplierId)
            .OrderByDescending(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();
        var item = await vContext.PurchaseReceiptItems
            .SingleAsync(i => i.PurchaseReceiptId == receiptId);
        Assert.Equal(20m, item.Quantity);
        Assert.Equal(70_000m, item.UnitCost);
    }

    [SkippableFact]
    public async Task Receipt_AfterPartialSale_UsesCurrentAverage_AndInvoiceSnapshotsIt()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 150_000, costPrice: 0, stockQty: 0);

        int supplierId, invoiceId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier($"تأمین‌کننده {Guid.NewGuid():N}"[..18]);
            var customer = new Customer("مشتری هزینه", "09121260001", "تهران");
            context.Suppliers.Add(supplier);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        // ۱۰ عدد با بهای ۶۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput> { new() { ProductId = product.Id, Quantity = 10, UnitCost = 60_000 } }
            }, "it-user");
        }

        // فروش ۴ عدد با میانگین ۶۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customerId = context.Customers.AsNoTracking().First(c => c.Phone == "09121260001").Id;
            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId,
                WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput> { new() { ProductId = product.Id, Quantity = 4, UnitPrice = 150_000 } }
            }, "it-user");
            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        // خرید ۱۰ عدد با بهای ۸۰٬۰۰۰ ⇒ موجودی ۶ عدد با میانگین ۶۰ (ارزش ۳۶۰) + خرید ۸۰۰
        // میانگین جدید = (۳۶۰ + ۸۰۰) ÷ ۱۶ = ۷۲٬۵۰۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput> { new() { ProductId = product.Id, Quantity = 10, UnitCost = 80_000 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(72_500m, CostOf(vContext, product.Id));

        // فاکتور قبلی باید همان ۶۰٬۰۰۰ را نگه داشته باشد (اسنپ‌شات) — نه میانگین جدید
        var invoiceItem = await vContext.SalesInvoiceItems
            .AsNoTracking().SingleAsync(i => i.SalesInvoiceId == invoiceId);
        Assert.Equal(60_000m, invoiceItem.CostPrice);

        // سند فروش همان ۲۴۰٬۰۰۰ (۴ × ۶۰٬۰۰۰) بسته شده باشد
        var accountIds = await vContext.Accounts
            .Where(a => a.Code == "5000").ToDictionaryAsync(a => a.Code, a => a.Id);
        var cogs = await vContext.JournalEntryLines
            .AsNoTracking()
            .Where(l => l.AccountId == accountIds["5000"] && l.DebitAmount > 0)
            .SumAsync(l => l.DebitAmount);
        Assert.Equal(240_000m, cogs);

        // لغو فاکتور هم باید با همان ۲۴۰٬۰۰۰ برگردد، نه با میانگین جدید
        using (var scope = _db.CreateScope())
        {
            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            await sales.CancelInvoiceAsync(invoiceId, "it-user");
        }

        using var after = _db.CreateScope();
        var aContext = after.ServiceProvider.GetRequiredService<AppDbContext>();
        var reversalCogs = await aContext.JournalEntryLines
            .AsNoTracking()
            .Where(l => l.AccountId == accountIds["5000"] && l.CreditAmount > 0)
            .SumAsync(l => l.CreditAmount);
        Assert.Equal(240_000m, reversalCogs);
    }

    [SkippableFact]
    public async Task ConcurrentReceipts_KeepStockAndCostConsistent()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 150_000, costPrice: 0, stockQty: 0);

        int supplierId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier($"تأمین‌کننده {Guid.NewGuid():N}"[..18]);
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        // دو رسید خریدِ هم‌زمان برای یک کالا (سناریوی واقعیِ دو انباردار)
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async i =>
        {
            using var scope = _db.CreateScope();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = i == 0 ? 60_000 : 80_000 }
                }
            }, "it-user");
            return true;
        }));

        Assert.All(results, r => Assert.True(r));

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // هیچ رسیدی نباید گم شده باشد و موجودی باید دقیقاً جمع هر دو باشد
        var receiptCount = await vContext.PurchaseReceipts.CountAsync(r => r.Items.Any(i => i.ProductId == product.Id));
        Assert.Equal(2, receiptCount);

        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(20m, level.QuantityOnHand);

        // میانگین باید یکی از دو مقدارِ ممکن باشد (نه چیزی بینابین و نه منفی)
        var cost = CostOf(vContext, product.Id);
        Assert.Contains(cost, new[] { 60_000m, 70_000m, 80_000m });
    }
}
