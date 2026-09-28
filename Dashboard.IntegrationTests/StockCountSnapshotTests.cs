using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// انبارگردانی: مبنای اختلاف باید «موجودیِ لحظهٔ بستن» باشد، نه اسنپ‌شاتِ لحظهٔ باز کردن.
/// </summary>
[Collection("Database")]
public class StockCountSnapshotTests
{
    private readonly TestDatabaseFixture _db;

    public StockCountSnapshotTests(TestDatabaseFixture db) => _db = db;

    /// <summary>
    /// سناریوی بازتولید باگ:
    /// موجودی ۱۰۰ → شمارش باز می‌شود (اسنپ‌شات ۱۰۰) → وسط شمارش ۴۰ عدد از انبار
    /// منتقل می‌شود (موجودی واقعی ۶۰) → شمارشگر عددِ درست را می‌شمارد (۱۰۰).
    ///
    /// با کد معیوب: Discrepancy = ۱۰۰ − ۱۰۰ = ۰ ⇒ هیچ تراکنشی ثبت نمی‌شود و
    /// موجودی روی ۱۰۰ می‌ماند در حالی که واقعاً ۶۰ است (۴۰ عدد گم‌شده، بی‌سروصدا).
    /// با کد درست: اختلاف ۴۰− واحد کسر می‌شود و موجودی به ۶۰ می‌رسد.
    /// </summary>
    [SkippableFact]
    public async Task ClosingCount_UsesCurrentStock_NotOpeningSnapshot()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 100);

        int countId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();

            var opened = await stock.OpenStockCountAsync(1, "it-user");
            countId = opened.Id;

            // موجودیِ سیستم در لحظهٔ باز کردن = ۱۰۰ (اسنپ‌شات)
            Assert.Equal(100m, opened.Items.Single(i => i.ProductId == product.Id).SystemQuantity);
        }

        // وسط شمارش، ۴۰ عدد از همین انبار به انبار ۲ منتقل می‌شود
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Warehouses.Add(new Warehouse
            {
                Name = "انبار مقصد تست", Code = $"DST-{Guid.NewGuid():N}"[..16],
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            var destId = ctx.Warehouses.Where(w => w.Code.StartsWith("DST-")).Select(w => w.Id).Single();

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.RegisterStockTransferAsync(new CreateStockTransferDto
            {
                SourceWarehouseId = 1,
                DestinationWarehouseId = destId,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 40 } }
            }, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();

            // شمارشگر انبار را می‌شمارد و ۶۰ عدد می‌بیند (۴۰ عدد واقعاً رفته بود).
            // اگر مبنا اسنپ‌شات (۱۰۰) باشد، اختلاف ۶۰−۱۰۰ = −۴۰ می‌شود و سیستم
            // موجودی را به ۶۰−۴۰ = ۲۰ می‌رساند؛ یعنی ۴۰ عدد دیگر هم بی‌دلیل کم می‌شود
            // (انتقالِ واقعی دو بار اعمال می‌شود). با مبنای درست، اختلاف صفر است.
            await stock.CloseStockCountAsync(countId, new Dictionary<int, decimal> { [product.Id] = 60 }, "it-user");
        }

        using var verify = _db.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var level = await db.StockLevels.SingleAsync(l => l.ProductId == product.Id && l.WarehouseId == 1);

        // موجودی باید همان ۶۰ واقعی بماند — نه ۲۰ (که نشانهٔ اعمال دوبارهٔ انتقال است)
        Assert.Equal(60m, level.QuantityOnHand);

        // شمارشگر با واقعیت هم‌خوان بود ⇒ هیچ اصلاحی لازم نیست
        var tx = await db.StockTransactions
            .Where(t => t.ProductId == product.Id && t.ReferenceType == nameof(StockCount))
            .ToListAsync();
        Assert.Empty(tx);

        // و اسنپ‌شاتِ سند به‌روز شده تا گزارش/نمایش هم عدد واقعی را نشان دهد
        var closed = await db.StockCounts.Include(c => c.Items).FirstAsync(c => c.Id == countId);
        Assert.Equal(60m, closed.Items.Single(i => i.ProductId == product.Id).SystemQuantity);
    }

    /// <summary>
    /// جهت مخالفِ همان باگ: کالایی که بعد از شروع شمارش *اضافه* شده. اگر مبنا
    /// اسنپ‌شات بماند، سیستم موجودی را از واقعی بیشتر می‌کند (۱۰۰ به‌جای ۱۵۰).
    /// </summary>
    [SkippableFact]
    public async Task ClosingCount_DoesNotOverstateStock_WhenGoodsArriveMidCount()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 50);

        int countId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            countId = (await stock.OpenStockCountAsync(1, "it-user")).Id;
        }

        // وسط شمارش، ۱۰۰ عدد رسید خریدِ جدید می‌رسد ⇒ موجودی واقعی ۱۵۰
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            var supplierId = await EnsureSupplierAsync(scope);
            await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                ReceiptDate = DateTime.UtcNow,
                Items = new List<PurchaseReceiptItemInput> { new() { ProductId = product.Id, Quantity = 100, UnitCost = 7_000 } }
            }, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stock.CloseStockCountAsync(countId, new Dictionary<int, decimal> { [product.Id] = 150 }, "it-user");
        }

        using var verify = _db.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var level = await db.StockLevels.SingleAsync(l => l.ProductId == product.Id && l.WarehouseId == 1);

        Assert.Equal(150m, level.QuantityOnHand);
    }

    private static async Task<int> EnsureSupplierAsync(IServiceScope scope)
    {
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = new Supplier { Name = "تأمین‌کنندهٔ تست" };
        ctx.Suppliers.Add(s);
        await ctx.SaveChangesAsync();
        return s.Id;
    }
}
