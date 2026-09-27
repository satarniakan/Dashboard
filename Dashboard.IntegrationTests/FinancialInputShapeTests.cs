using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
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
/// مرحلهٔ ۲ بازبینی: «شکل داده» در سرویس‌های مالیِ بدون پوشش تست.
/// الگویی که در همهٔ باگ‌های پیدا‌شده تکرار شد: سطر تکراری، مقدار منفی، مرز صفر.
/// همین سه سناریو اینجا روی کد تخفیف و اقساط امتحان می‌شود.
/// </summary>
[Collection("Database")]
public class FinancialInputShapeTests
{
    private readonly TestDatabaseFixture _db;
    public FinancialInputShapeTests(TestDatabaseFixture db) => _db = db;

    // ---------------- کد تخفیف: قوانین محاسبه (بدون دیتابیس) ----------------

    /// <summary>کدِ «مبلغ ثابت» بزرگ‌تر از سبد نباید جمع منفی بدهد؛ CalculateDiscount باید کلمپ کند.</summary>
    [Fact]
    public void DiscountCode_FixedAmountLargerThanCart_ClampsToCart()
    {
        var code = new DiscountCode { Code = "BIG", Type = DiscountType.FixedAmount, Value = 1_000_000m };
        var cartTotal = 100_000m;
        Assert.InRange(code.CalculateDiscount(cartTotal), 0m, cartTotal);
    }

    /// <summary>درصد ۱۰۰ نباید از کل سبد بیشتر شود.</summary>
    [Fact]
    public void DiscountCode_Percentage100_NeverExceedsCart()
    {
        var code = new DiscountCode { Code = "FULL", Type = DiscountType.Percentage, Value = 100m };
        Assert.Equal(100_000m, code.CalculateDiscount(100_000m));
    }

    /// <summary>سقف مبلغ تخفیف برای کد درصدی باید رعایت شود.</summary>
    [Fact]
    public void DiscountCode_MaxDiscountAmount_Respected()
    {
        var code = new DiscountCode
        {
            Code = "CAP", Type = DiscountType.Percentage, Value = 50m, MaxDiscountAmount = 20_000m
        };
        // ۵۰٪ از ۱۰۰٬۰۰۰ = ۵۰٬۰۰۰ ولی سقف ۲۰٬۰۰۰ است
        Assert.Equal(20_000m, code.CalculateDiscount(100_000m));
    }

    /// <summary>حداقل مبلغ سبد: زیر آن، تخفیف اعمال نمی‌شود.</summary>
    [Fact]
    public void DiscountCode_BelowMinCart_ReturnsZero()
    {
        var code = new DiscountCode
        {
            Code = "MIN", Type = DiscountType.Percentage, Value = 20m, MinCartAmount = 500_000m
        };
        Assert.Equal(0m, code.CalculateDiscount(100_000m));
    }

    /// <summary>کد منقضی نباید معتبر باشد.</summary>
    [Fact]
    public void DiscountCode_Expired_IsNotValid()
    {
        var code = new DiscountCode
        {
            Code = "OLD", Type = DiscountType.Percentage, Value = 10m,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        Assert.False(code.IsValidNow(DateTime.UtcNow));
    }

    /// <summary>کد با سقف مصرف پرشده نباید معتبر باشد.</summary>
    [Fact]
    public void DiscountCode_MaxUsageReached_IsNotValid()
    {
        var code = new DiscountCode
        {
            Code = "USED", Type = DiscountType.Percentage, Value = 10m,
            MaxUsageCount = 5, UsageCount = 5
        };
        Assert.False(code.IsValidNow(DateTime.UtcNow));
    }

    /// <summary>کد غیرفعال نباید معتبر باشد.</summary>
    [Fact]
    public void DiscountCode_Inactive_IsNotValid()
    {
        var code = new DiscountCode { Code = "OFF", Type = DiscountType.Percentage, Value = 10m, IsActive = false };
        Assert.False(code.IsValidNow(DateTime.UtcNow));
    }

    /// <summary>مرز صفر: تخفیف صفر، نه منفی.</summary>
    [Fact]
    public void DiscountCode_ZeroValue_ReturnsZero()
    {
        var code = new DiscountCode { Code = "ZERO", Type = DiscountType.Percentage, Value = 0m };
        Assert.Equal(0m, code.CalculateDiscount(100_000m));
    }

    // ---------------- کد تخفیس: اعتبارسنجی سرویس (با دیتابیس) ----------------

    /// <summary>درصد بیشتر از ۱۰۰ نباید ثبت شود.</summary>
    [SkippableFact]
    public async Task DiscountCode_PercentageOver100_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateAsync(new CreateDiscountCodeDto
        {
            Code = "OVER100", Type = DiscountType.Percentage, Value = 150m
        }));
    }

    /// <summary>مقدار منفی نباید ذخیره شود.</summary>
    [SkippableFact]
    public async Task DiscountCode_NegativeValue_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateAsync(new CreateDiscountCodeDto
        {
            Code = "NEGVAL", Type = DiscountType.Percentage, Value = -10m
        }));
    }

    /// <summary>کد تکراری (با حروف کوچک) نباید ثبت شود — نرمال‌سازی به حروف بزرگ.</summary>
    [SkippableFact]
    public async Task DiscountCode_DuplicateCodeDifferentCase_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var unique = $"DUP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        using (var scope = _db.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IDiscountCodeService>();
            await svc.CreateAsync(new CreateDiscountCodeDto
            {
                Code = unique, Type = DiscountType.Percentage, Value = 10m
            });
        }

        using var dup = _db.CreateScope();
        var dupSvc = dup.ServiceProvider.GetRequiredService<IDiscountCodeService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => dupSvc.CreateAsync(new CreateDiscountCodeDto
        {
            Code = unique.ToLowerInvariant(), Type = DiscountType.Percentage, Value = 20m
        }));
    }

    // ---------------- اقساط ----------------

    private async Task<(int InvoiceId, int CustomerId)> SeedConfirmedInvoiceAsync(string phone)
    {
        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer("تست اقساط", phone, "تهران");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
        var invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id, WarehouseId = 1,
            Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 1, UnitPrice = 100_000m } }
        }, "u");
        await sales.ConfirmInvoiceAsync(invoiceId, "u");
        return (invoiceId, customer.Id);
    }

    /// <summary>جمع اقساط باید دقیقاً برابر مبلغ فاکتور باشد.</summary>
    [SkippableFact]
    public async Task InstallmentPlan_SumNotEqualInvoice_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var (invoiceId, _) = await SeedConfirmedInvoiceAsync("09121294004");

        using var plan = _db.CreateScope();
        var svc = plan.ServiceProvider.GetRequiredService<IInstallmentService>();
        // جمع اقساط ۹۰٬۰۰۰ است ولی فاکتور ۱۰۰٬۰۰۰
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateInstallmentPlanAsync(new CreateInstallmentPlanDto
        {
            SalesInvoiceId = invoiceId,
            Installments = new List<InstallmentInput>
            {
                new(DateTime.UtcNow.AddDays(30), 90_000m)
            }
        }, "u"));
    }

    /// <summary>قسط با مبلغ صفر نباید پذیرفته شود.</summary>
    [SkippableFact]
    public async Task InstallmentPlan_ZeroAmount_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var (invoiceId, _) = await SeedConfirmedInvoiceAsync("09121294005");

        using var plan = _db.CreateScope();
        var svc = plan.ServiceProvider.GetRequiredService<IInstallmentService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateInstallmentPlanAsync(new CreateInstallmentPlanDto
        {
            SalesInvoiceId = invoiceId,
            Installments = new List<InstallmentInput>
            {
                new(DateTime.UtcNow.AddDays(30), 100_000m),
                new(DateTime.UtcNow.AddDays(60), 0m)
            }
        }, "u"));
    }

    /// <summary>سررسید در گذشته نباید پذیرفته شود.</summary>
    [SkippableFact]
    public async Task InstallmentPlan_PastDueDate_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var (invoiceId, _) = await SeedConfirmedInvoiceAsync("09121294006");

        using var plan = _db.CreateScope();
        var svc = plan.ServiceProvider.GetRequiredService<IInstallmentService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateInstallmentPlanAsync(new CreateInstallmentPlanDto
        {
            SalesInvoiceId = invoiceId,
            Installments = new List<InstallmentInput>
            {
                new(DateTime.UtcNow.AddDays(-5), 100_000m)
            }
        }, "u"));
    }

    // ---------------- مرحلهٔ ۳: ریپازیتوری موجودی (پایهٔ اعداد فروشگاه) ----------------

    /// <summary>موجودی قابل‌فروش = موجودی فیزیکی منهای رزروها.</summary>
    [SkippableFact]
    public async Task GetAvailableForSale_SubtractsReservations()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        using (var scope = _db.CreateScope())
        {
            var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
            Assert.True(await stockLevels.TryReserveAsync(product.Id, 1, 4m));
        }

        using var verify = _db.CreateScope();
        var vStock = verify.ServiceProvider.GetRequiredService<IStockLevelRepository>();
        var available = await vStock.GetAvailableForSaleAsync(new[] { product.Id }, 1);

        Assert.True(available.TryGetValue(product.Id, out var q), "کالا باید در دیکشنری باشد");
        Assert.Equal(6m, q); // ۱۰ فیزیکی − ۴ رزرو
    }

    /// <summary>رزرو بیش از موجودیِ آزاد باید ناموفق باشد (شرط در همان UPDATE).</summary>
    [SkippableFact]
    public async Task TryReserve_MoreThanAvailable_ReturnsFalse()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        using var scope = _db.CreateScope();
        var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();

        Assert.False(await stockLevels.TryReserveAsync(product.Id, 1, 6m));
    }

    /// <summary>دو رزروِ پشت‌سرهم نباید از موجودی آزاد فراتر بروند.</summary>
    [SkippableFact]
    public async Task TryReserve_TwiceInRow_SecondFailsWhenExhausted()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        using (var scope = _db.CreateScope())
        {
            var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
            Assert.True(await stockLevels.TryReserveAsync(product.Id, 1, 5m));
        }

        using var second = _db.CreateScope();
        var secondStock = second.ServiceProvider.GetRequiredService<IStockLevelRepository>();
        Assert.False(await secondStock.TryReserveAsync(product.Id, 1, 1m));
    }

    /// <summary>آزادسازی رزرو باید موجودی قابل‌فروش را دقیقاً برگرداند.</summary>
    [SkippableFact]
    public async Task ReleaseReservation_RestoresAvailability()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 8);
        using (var scope = _db.CreateScope())
        {
            var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
            Assert.True(await stockLevels.TryReserveAsync(product.Id, 1, 3m));
            await stockLevels.ReleaseReservationAsync(product.Id, 1, 3m);

            var available = await stockLevels.GetAvailableForSaleAsync(new[] { product.Id }, 1);
            Assert.Equal(8m, available[product.Id]);
        }
    }

    /// <summary>موجودی کل باید جمع همهٔ انبارها باشد.</summary>
    [SkippableFact]
    public async Task GetTotalStock_SumsAcrossWarehouses()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 7, warehouseId: 1);

        // شناسهٔ انبار دوم را از خود دیتابیس می‌گیریم (Id خودکار است و تضمیناً ۲ نیست)
        int secondWarehouseId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wh2 = await ctx.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-2");
            if (wh2 is null)
            {
                wh2 = new Warehouse("انبار دوم", "WH-2");
                ctx.Warehouses.Add(wh2);
                await ctx.SaveChangesAsync();
            }
            secondWarehouseId = wh2.Id;

            var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
            await stockLevels.IncreaseOrCreateAsync(product.Id, secondWarehouseId, 5m);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }

        using var verify = _db.CreateScope();
        var vStock = verify.ServiceProvider.GetRequiredService<IStockLevelRepository>();
        var totals = await vStock.GetTotalStockAsync(new[] { product.Id });

        Assert.Equal(12m, totals[product.Id]); // ۷ + ۵
    }

    /// <summary>موجودی یک انبار نباید با انبار دیگر قاطی شود.</summary>
    [SkippableFact]
    public async Task GetWarehouseStock_IsolatesWarehouse()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 9, warehouseId: 1);

        using var scope = _db.CreateScope();
        var stockLevels = scope.ServiceProvider.GetRequiredService<IStockLevelRepository>();
        var byWarehouse = await stockLevels.GetWarehouseStockAsync(new[] { product.Id }, 1);

        Assert.Equal(9m, byWarehouse[product.Id]);
    }
}
