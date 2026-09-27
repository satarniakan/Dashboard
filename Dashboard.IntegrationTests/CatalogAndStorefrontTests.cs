using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// ادامهٔ بازبینی: کاتالوگ (واحد شمارش و…) و ویترین فروشگاه.
/// این‌ها ورودی کاربر می‌گیرند و خروجی‌شان مستقیم دیده می‌شود.
/// </summary>
[Collection("Database")]
public class CatalogAndStorefrontTests
{
    private readonly TestDatabaseFixture _db;
    public CatalogAndStorefrontTests(TestDatabaseFixture db) => _db = db;

    private static string NewCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..10];

    /// <summary>
    /// نگهبانِ زیرساخت تست: تضمین می‌کند پاک‌سازی دادهٔ بین تست‌ها اسکیمای دیتابیس را
    /// خراب نکند. اگر قیدهای خارجی بازسازی نشوند، همهٔ تست‌ها سبز می‌شوند ولی
    /// دیتابیس دیگر هیچ بازرسیِ صحتِ داده‌ای ندارد — یعنی «تست سبزِ توهمی».
    /// </summary>
    [SkippableFact]
    public async Task Reset_KeepsSchemaIntact_AndReferenceData()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        async Task<int> ScalarAsync(string sql)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            var v = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(v);
        }

        // قیدهای خارجی باید همان تعداد اولیه باشند و هیچ‌کدام غیرفعال نباشند
        var fkCount = await ScalarAsync("SELECT COUNT(*) FROM sys.foreign_keys");
        var notTrusted = await ScalarAsync("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1");
        Assert.True(fkCount > 50, $"تعداد قیدهای خارجی غیرمنتظره است: {fkCount}");
        Assert.Equal(0, notTrusted);

        // جدول‌های مرجع باید حفظ شده باشند
        Assert.True(await ScalarAsync("SELECT COUNT(*) FROM Warehouses") >= 1, "انبار مرجع از بین رفته است.");
        Assert.True(await ScalarAsync("SELECT COUNT(*) FROM Accounts WHERE IsSystemAccount = 1") >= 5,
            "سرفصل‌های حسابداری از بین رفته‌اند.");

        // جداول کاربردی باید خالی باشند (ایزوله‌شدن)
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM Products"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM SalesInvoices"));
    }

    // ---------------- واحد شمارش ----------------

    /// <summary>نام واحد خالی یا فقط فاصله نباید پذیرفته شود.</summary>
    [SkippableFact]
    public async Task CreateUnit_EmptyName_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => catalog.CreateUnitAsync(new CreateUnitDto { Name = "   " }));
    }

    /// <summary>نام واحد تکراری نباید ثبت شود.</summary>
    [SkippableFact]
    public async Task CreateUnit_DuplicateName_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var name = NewCode("U");
        using (var scope = _db.CreateScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            await catalog.CreateUnitAsync(new CreateUnitDto { Name = name });
        }

        using var dup = _db.CreateScope();
        var dupCatalog = dup.ServiceProvider.GetRequiredService<ICatalogService>();
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => dupCatalog.CreateUnitAsync(new CreateUnitDto { Name = name }));
    }

    /// <summary>واحدِ در حال استفاده نباید حذف شود.</summary>
    [SkippableFact]
    public async Task DeleteUnit_InUseByProduct_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var name = NewCode("U");
        int unitId;
        using (var scope = _db.CreateScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            await catalog.CreateUnitAsync(new CreateUnitDto { Name = name });
            unitId = (await catalog.GetUnitsAsync()).First(u => u.Name == name).Id;

            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Add(new Product($"SKU-{Guid.NewGuid():N}"[..20], "کالای واحد", 1000m, 500m, name));
            await ctx.SaveChangesAsync();
        }

        using var del = _db.CreateScope();
        var delCatalog = del.ServiceProvider.GetRequiredService<ICatalogService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => delCatalog.DeleteUnitAsync(unitId));
    }

    /// <summary>
    /// تغییر نام واحد باید روی کالاهای موجود هم اعمال شود، وگرنه محصول‌ها به واحد
    /// ناموجودی اشاره می‌کنند.
    /// </summary>
    [SkippableFact]
    public async Task UpdateUnit_Rename_CascadesToProducts()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var oldName = NewCode("U");
        var newName = NewCode("V");
        int unitId;
        int productId;

        using (var scope = _db.CreateScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            await catalog.CreateUnitAsync(new CreateUnitDto { Name = oldName });
            unitId = (await catalog.GetUnitsAsync()).First(u => u.Name == oldName).Id;

            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = new Product($"SKU-{Guid.NewGuid():N}"[..20], "کالای ری‌نیم", 1000m, 500m, oldName);
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            productId = product.Id;

            await catalog.UpdateUnitAsync(unitId, new UpdateUnitDto { Name = newName });
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await vCtx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(newName, reloaded.Unit);
    }

    /// <summary>واحد بدون مصرف باید حذف شود.</summary>
    [SkippableFact]
    public async Task DeleteUnit_Unused_Succeeds()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var name = NewCode("U");
        int unitId;
        using (var scope = _db.CreateScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            await catalog.CreateUnitAsync(new CreateUnitDto { Name = name });
            unitId = (await catalog.GetUnitsAsync()).First(u => u.Name == name).Id;
        }

        using var del = _db.CreateScope();
        var delCatalog = del.ServiceProvider.GetRequiredService<ICatalogService>();
        await delCatalog.DeleteUnitAsync(unitId);

        Assert.False((await delCatalog.GetUnitsAsync()).Any(u => u.Id == unitId));
    }

    /// <summary>تعداد کالاهای هر واحد باید درست گزارش شود.</summary>
    [SkippableFact]
    public async Task GetUnits_ReportsUsageCount()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var name = NewCode("U");
        using var scope = _db.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        await catalog.CreateUnitAsync(new CreateUnitDto { Name = name });

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Products.Add(new Product($"SKU-{Guid.NewGuid():N}"[..20], "کالا ۱", 1000m, 500m, name));
        ctx.Products.Add(new Product($"SKU-{Guid.NewGuid():N}"[..20], "کالا ۲", 2000m, 900m, name));
        await ctx.SaveChangesAsync();

        var unit = (await catalog.GetUnitsAsync()).First(u => u.Name == name);
        Assert.Equal(2, unit.ProductCount);
    }

    // ---------------- ویترین فروشگاه ----------------

    /// <summary>فقط کالای منتشرشده باید در ویترین دیده شود.</summary>
    [SkippableFact]
    public async Task GetProducts_OnlyPublished()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var published = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        var hidden = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        hidden.SetStoreDetails(false, $"hidden-{Guid.NewGuid():N}"[..16], null);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Update(hidden);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var store = verify.ServiceProvider.GetRequiredService<IStorefrontService>();
        var (items, _) = await store.GetProductsAsync(1, 50);

        var ids = items.Select(p => p.Id).ToList();
        Assert.Contains(published.Id, ids);
        Assert.DoesNotContain(hidden.Id, ids);
    }

    /// <summary>کالای بدون موجودی نباید «موجود» نمایش داده شود.</summary>
    [SkippableFact]
    public async Task GetProducts_OutOfStock_IsNotInStock()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var p = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 0);

        using var scope = _db.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStorefrontService>();
        var (items, _) = await store.GetProductsAsync(1, 50);

        var dto = items.FirstOrDefault(x => x.Id == p.Id);
        if (dto is not null) Assert.False(dto.InStock);
    }

    /// <summary>کالای کاملاً رزروشده (فیزیکی موجود، آزاد صفر) موجود نیست.</summary>
    [SkippableFact]
    public async Task GetProducts_FullyReserved_IsNotInStock()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var p = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 4);
        using (var scope = _db.CreateScope())
        {
            var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
            await stockLevels.TryReserveAsync(p.Id, 1, 4m);
        }

        using var verify = _db.CreateScope();
        var store = verify.ServiceProvider.GetRequiredService<IStorefrontService>();
        var (items, _) = await store.GetProductsAsync(1, 50);

        var dto = items.FirstOrDefault(x => x.Id == p.Id);
        if (dto is not null) Assert.False(dto.InStock);
    }

    /// <summary>Slug ناموجود نباید استثنا بدهد.</summary>
    [SkippableFact]
    public async Task GetProductBySlug_NotFound_ReturnsNull()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStorefrontService>();
        Assert.Null(await store.GetProductBySlugAsync($"nope-{Guid.NewGuid():N}"));
    }

    /// <summary>کالای منتشرنشده نباید با Slug باز شود.</summary>
    [SkippableFact]
    public async Task GetProductBySlug_Unpublished_ReturnsNull()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var p = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);
        var slug = $"hidden-{Guid.NewGuid():N}"[..16];
        p.SetStoreDetails(false, slug, null);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Update(p);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var store = verify.ServiceProvider.GetRequiredService<IStorefrontService>();
        Assert.Null(await store.GetProductBySlugAsync(slug));
    }

    /// <summary>کالای منتشرشده باید با Slug باز شود.</summary>
    [SkippableFact]
    public async Task GetProductBySlug_Published_ReturnsDetail()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var p = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);
        var slug = $"live-{Guid.NewGuid():N}"[..16];
        p.SetStoreDetails(true, slug, "<p>توضیح</p>");

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Update(p);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var store = verify.ServiceProvider.GetRequiredService<IStorefrontService>();
        var dto = await store.GetProductBySlugAsync(slug);

        Assert.NotNull(dto);
        Assert.Equal(p.Id, dto.Id);
        Assert.Equal(100_000m, dto.Price);
        Assert.True(dto.InStock);
    }

    /// <summary>کالای مرتبط نباید خودِ کالای جاری باشد.</summary>
    [SkippableFact]
    public async Task GetProductBySlug_Related_ExcludesSelf()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var a = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);
        var b = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);
        var slugA = $"rel-a-{Guid.NewGuid():N}"[..16];
        a.SetStoreDetails(true, slugA, null);

        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Products.Update(a);
            ctx.Products.Update(b);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var store = verify.ServiceProvider.GetRequiredService<IStorefrontService>();
        var dto = await store.GetProductBySlugAsync(slugA);

        Assert.NotNull(dto);
        Assert.DoesNotContain(dto.Related, r => r.Id == a.Id);
    }

    /// <summary>صفحهٔ نامعتبر نباید خطا بدهد.</summary>
    [SkippableFact]
    public async Task GetProducts_InvalidPage_DoesNotThrow()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStorefrontService>();
        var (items, _) = await store.GetProductsAsync(0, -5);
        Assert.NotNull(items);
    }
}
