using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// ادامهٔ بازبینی: سبد خرید — سرویسی که ورودیِ مستقیم کاربر می‌گیرد و پیش‌تر
/// هیچ تستی نداشت.
/// </summary>
[Collection("Database")]
public class CartAndProductTests
{
    private readonly TestDatabaseFixture _db;
    public CartAndProductTests(TestDatabaseFixture db) => _db = db;

    // ---------------- افزودن به سبد ----------------

    /// <summary>تعداد صفر یا منفی نباید پذیرفته شود (مرز ورودی کاربر).</summary>
    [SkippableFact]
    public async Task AddToCart_NonPositiveQuantity_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();

        Assert.False((await cart.AddToCartAsync("c-nq", product.Id, 0m)).Success);
        Assert.False((await cart.AddToCartAsync("c-nq", product.Id, -5m)).Success);
    }

    /// <summary>کالای ناموجود نباید به سبد اضافه شود.</summary>
    [SkippableFact]
    public async Task AddToCart_NonexistentProduct_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        Assert.False((await cart.AddToCartAsync("c-missing", 999_999, 1m)).Success);
    }

    /// <summary>مقدار اضافه‌شده نباید از موجودیِ قابل‌فروش بیشتر شود (clamp).</summary>
    [SkippableFact]
    public async Task AddToCart_MoreThanStock_ClampsToAvailable()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var result = await cart.AddToCartAsync("c-clamp", product.Id, 10m);

        Assert.True(result.Success);
        Assert.Equal(3m, await cart.GetItemCountAsync("c-clamp"));
    }

    /// <summary>افزودن دوبارهٔ همان کالا باید جمع شود، نه سطر تکراری بسازد.</summary>
    [SkippableFact]
    public async Task AddToCart_SameProductTwice_MergesIntoOneLine()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        await cart.AddToCartAsync("c-merge", product.Id, 2m);
        await cart.AddToCartAsync("c-merge", product.Id, 3m);

        var dto = await cart.GetCartAsync("c-merge");
        Assert.Single(dto.Items);
        Assert.Equal(5m, dto.Items[0].Quantity);
    }

    /// <summary>کالای بدون موجودی نباید اضافه شود.</summary>
    [SkippableFact]
    public async Task AddToCart_OutOfStock_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 0);

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        Assert.False((await cart.AddToCartAsync("c-oos", product.Id, 1m)).Success);
    }

    // ---------------- به‌روزرسانی و حذف ----------------

    /// <summary>تعداد صفر در به‌روزرسانی یعنی «حذف از سبد».</summary>
    [SkippableFact]
    public async Task UpdateQuantity_Zero_RemovesItem()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        const string cookie = "c-upd0";

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        await cart.AddToCartAsync(cookie, product.Id, 2m);
        var dto = await cart.GetCartAsync(cookie);

        var res = await cart.UpdateQuantityAsync(cookie, dto.Items[0].ItemId, 0m);
        Assert.True(res.Success);
        Assert.Equal(0m, await cart.GetItemCountAsync(cookie));
    }

    /// <summary>کالایی که در سبد نیست نباید قابل به‌روزرسانی باشد.</summary>
    [SkippableFact]
    public async Task UpdateQuantity_UnknownItem_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        Assert.False((await cart.UpdateQuantityAsync("c-none", 4242, 3m)).Success);
    }

    /// <summary>حذف قلمِ ناموجود نباید استثنا بدهد.</summary>
    [SkippableFact]
    public async Task RemoveItem_UnknownItem_DoesNotThrow()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        await cart.RemoveItemAsync("c-rm", 9999);
    }

    // ---------------- تخفیف روی سبد ----------------

    /// <summary>روی سبد خالی نمی‌توان کد تخفیف اعمال کرد.</summary>
    [SkippableFact]
    public async Task ApplyDiscount_EmptyCart_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        Assert.False((await cart.ApplyDiscountCodeAsync("c-empty", "ANYCODE")).Success);
    }

    /// <summary>کد ناموجود نباید اعمال شود.</summary>
    [SkippableFact]
    public async Task ApplyDiscount_UnknownCode_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        const string cookie = "c-dc";

        using var scope = _db.CreateScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        await cart.AddToCartAsync(cookie, product.Id, 1m);
        Assert.False((await cart.ApplyDiscountCodeAsync(cookie, "NOSUCHCODE")).Success);
    }

    /// <summary>کد معتبر باید روی سبد بنشیند و جمع سبد را کم کند.</summary>
    [SkippableFact]
    public async Task ApplyDiscount_ValidCode_AppliesToCart()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        var code = $"OK{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        const string cookie = "c-dc-ok";

        using (var scope = _db.CreateScope())
        {
            var discounts = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
            await discounts.CreateAsync(new CreateDiscountCodeDto
            {
                Code = code, Type = DiscountType.Percentage, Value = 10m
            });
        }

        using (var scope = _db.CreateScope())
        {
            var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
            await cart.AddToCartAsync(cookie, product.Id, 2m);
            var res = await cart.ApplyDiscountCodeAsync(cookie, code);

            Assert.True(res.Success, res.Message);
            var dto = await cart.GetCartAsync(cookie);
            // TotalAmount جمع خام است؛ مبلغ قابل پرداخت در FinalAmount می‌آید
            Assert.Equal(200_000m, dto.TotalAmount);
            Assert.Equal(20_000m, dto.DiscountAmount);
            Assert.Equal(180_000m, dto.FinalAmount); // ۱۰٪ از ۲۰۰٬۰۰۰
        }
    }

    /// <summary>کد زیر حداقل مبلغ سبد نباید اعمال شود.</summary>
    [SkippableFact]
    public async Task ApplyDiscount_BelowMinCart_Fails()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        var code = $"MIN{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        const string cookie = "c-dc-min";

        using (var scope = _db.CreateScope())
        {
            var discounts = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
            await discounts.CreateAsync(new CreateDiscountCodeDto
            {
                Code = code, Type = DiscountType.Percentage, Value = 10m, MinCartAmount = 10_000_000m
            });
        }

        using (var scope = _db.CreateScope())
        {
            var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
            await cart.AddToCartAsync(cookie, product.Id, 1m);
            Assert.False((await cart.ApplyDiscountCodeAsync(cookie, code)).Success);
        }
    }

    /// <summary>حذف کد تخفیف باید سبد را به حالت بدون تخفیف برگرداند.</summary>
    [SkippableFact]
    public async Task RemoveDiscountCode_RestoresFullTotal()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        var code = $"RM{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        const string cookie = "c-dc-rm";

        using (var scope = _db.CreateScope())
        {
            var discounts = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
            await discounts.CreateAsync(new CreateDiscountCodeDto
            {
                Code = code, Type = DiscountType.Percentage, Value = 10m
            });
        }

        using (var scope = _db.CreateScope())
        {
            var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
            await cart.AddToCartAsync(cookie, product.Id, 2m);
            await cart.ApplyDiscountCodeAsync(cookie, code);
            await cart.RemoveDiscountCodeAsync(cookie);

            var dto = await cart.GetCartAsync(cookie);
            Assert.Equal(200_000m, dto.TotalAmount);
            Assert.Equal(0m, dto.DiscountAmount);
            Assert.Equal(200_000m, dto.FinalAmount);
        }
    }

    // ---------------- محصولات: قوانین دامنه (بدون دیتابیس) ----------------

    /// <summary>قیمت منفی نباید پذیرفته شود.</summary>
    [Fact]
    public void Product_NegativePrice_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Product("SKU-1", "کالا", -1m, 10m));
    }

    /// <summary>بهای تمام‌شدهٔ منفی نباید پذیرفته شود.</summary>
    [Fact]
    public void Product_NegativeCostPrice_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Product("SKU-1", "کالا", 100m, -5m));
    }

    /// <summary>کد کالای خالی نباید پذیرفته شود.</summary>
    [Fact]
    public void Product_EmptySku_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Product("   ", "کالا", 100m, 10m));
    }

    /// <summary>تخفیف بیش از ۱۰۰٪ نباید قیمت را منفی کند.</summary>
    [Fact]
    public void Product_DiscountOver100_Throws()
    {
        var p = new Product("SKU-D", "کالا", 100m, 10m);
        Assert.Throws<ArgumentException>(() => p.ApplyDiscount(150m));
    }

    /// <summary>تخفیف ۱۰۰٪ قیمت را صفر می‌کند، نه منفی.</summary>
    [Fact]
    public void Product_Discount100_ResultsInZeroPrice()
    {
        var p = new Product("SKU-F", "کالا", 100_000m, 10m);
        p.ApplyDiscount(100m);
        Assert.Equal(0m, p.Price);
    }

    /// <summary>کالای بدون Slug نمی‌تواند منتشر باشد.</summary>
    [Fact]
    public void Product_PublishWithoutSlug_StaysUnpublished()
    {
        var p = new Product("SKU-S", "کالا", 100m, 10m);
        p.SetStoreDetails(true, "   ", null);
        Assert.False(p.IsPublished);
    }

    /// <summary>Slug خالی به null تبدیل می‌شود (نه رشتهٔ فاصله).</summary>
    [Fact]
    public void Product_SetStoreDetails_NormalizesSlug()
    {
        var p = new Product("SKU-N", "کالا", 100m, 10m);
        p.SetStoreDetails(true, "  my-product  ", null);
        Assert.Equal("my-product", p.Slug);
        Assert.True(p.IsPublished);
    }

    /// <summary>تغییر واحد شمارش با مقدار خالی نباید پذیرفته شود.</summary>
    [Fact]
    public void Product_RenameUnit_RejectsEmpty()
    {
        var p = new Product("SKU-U", "کالا", 100m, 10m);
        Assert.Throws<ArgumentException>(() => p.RenameUnit("  "));
    }

    // ---------------- محصولات: یکتایی Slug (با دیتابیس) ----------------

    /// <summary>دو محصول نباید Slug یکسان داشته باشند.</summary>
    [SkippableFact]
    public async Task Product_DuplicateSlug_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var a = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);
        var b = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);
        var slug = $"dup-{Guid.NewGuid():N}"[..16];

        using (var scope = _db.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductService>();
            await products.UpdateProductAsync(a.Id, new UpdateProductDto
            {
                Name = "الف", Price = 100_000m, Sku = a.Sku, Unit = "عدد", CostPrice = 60_000m,
                IsPublished = true, Slug = slug
            }, "u");

            // کالای دوم با همان Slug باید رد شود
            await Assert.ThrowsAsync<BusinessRuleException>(() => products.UpdateProductAsync(b.Id, new UpdateProductDto
            {
                Name = "ب", Price = 100_000m, Sku = b.Sku, Unit = "عدد", CostPrice = 60_000m,
                IsPublished = true, Slug = slug
            }, "u"));
        }
    }

    /// <summary>ویرایش با Slug خودش نباید خطای تکراری بدهد (مستثنا شدن شناسهٔ خودش).</summary>
    [SkippableFact]
    public async Task Product_UpdateWithOwnSlug_Succeeds()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var p = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);
        var slug = $"own-{Guid.NewGuid():N}"[..16];

        using var scope = _db.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();

        // بار اول: slug را می‌گذارد
        await products.UpdateProductAsync(p.Id, new UpdateProductDto
        {
            Name = "الف", Price = 110_000m, Sku = p.Sku, Unit = "عدد", CostPrice = 60_000m,
            IsPublished = true, Slug = slug
        }, "u");

        // بار دوم: همان slug روی همان کالا ⇒ نباید خطا بدهد
        var result = await products.UpdateProductAsync(p.Id, new UpdateProductDto
        {
            Name = "ب", Price = 120_000m, Sku = p.Sku, Unit = "عدد", CostPrice = 60_000m,
            IsPublished = true, Slug = slug
        }, "u");

        Assert.NotNull(result);
    }

    /// <summary>ویرایش کالای ناموجود null برمی‌گرداند (نه استثنا).</summary>
    [SkippableFact]
    public async Task Product_UpdateNonExistent_ReturnsNull()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        Assert.Null(await products.UpdateProductAsync(999_999, new UpdateProductDto
        {
            Name = "x", Price = 1m, Sku = "K", Unit = "عدد", CostPrice = 1m
        }, "u"));
    }
}
