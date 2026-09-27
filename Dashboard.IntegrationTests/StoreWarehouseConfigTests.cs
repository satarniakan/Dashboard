using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// انبار فروشگاه («Store:WarehouseId») خودکار ساخته نمی‌شود. اگر تنظیم به انبارِ ناموجود
/// اشاره کند، موجودی صفر خوانده می‌شود و کاربر پیام گمراه‌کنندهٔ «موجودی کافی نیست»
/// می‌دید در حالی که مشکل از تنظیمات است. این تست‌ها هم پیام دقیق را قفل می‌کنند و هم
/// مطمئن می‌شوند همان چک، مسیر سالم ثبت سفارش را نمی‌شکند.
/// </summary>
[Collection("Database")]
public class StoreWarehouseConfigTests
{
    private readonly TestDatabaseFixture _db;

    public StoreWarehouseConfigTests(TestDatabaseFixture db) => _db = db;

    private static OrderService BuildOrderService(IServiceScope scope, int warehouseId) => new(
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
        scope.ServiceProvider.GetRequiredService<ISalesService>(),
        scope.ServiceProvider.GetRequiredService<IOutboxService>(),
        scope.ServiceProvider.GetRequiredService<INotificationService>(),
        Options.Create(new StoreOptions { WarehouseId = warehouseId }),
        scope.ServiceProvider.GetRequiredService<ITreasuryService>(),
        scope.ServiceProvider.GetRequiredService<ILogger<OrderService>>());

    private static CheckoutDto Checkout() => new()
    {
        CustomerName = "کاربر تست",
        CustomerPhone = "09121234567",
        Province = "تهران",
        City = "تهران",
        AddressLine = "خیابان تست، پلاک ۱",
        ShippingMethod = ShippingMethod.Post
    };

    [SkippableFact]
    public async Task PlaceOrder_WhenStoreWarehouseMissing_ReportsWarehouseError_InsteadOfStockError()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // کالا در انبار واقعی (۱) موجودی کافی دارد، ولی تنظیم به انبارِ ناموجود است
        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);

        using var scope = _db.CreateScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.AddToCartAsync("cookie-missing-wh", product.Id, 1);

        var orders = BuildOrderService(scope, warehouseId: 987654);
        var (order, error) = await orders.PlaceOrderAsync(
            "it-missing-wh-user", "cookie-missing-wh", Checkout());

        Assert.Null(order);
        Assert.NotNull(error);
        Assert.Contains("انبار فروشگاه", error);
        // پیام نباید «موجودی کافی نیست» باشد، وگرنه مدیر به‌جای تنظیمات سراغ انبار می‌رود
        Assert.DoesNotContain("موجودی", error);
    }

    [SkippableFact]
    public async Task PlaceOrder_WhenStoreWarehouseExists_StillSucceeds()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);

        using var scope = _db.CreateScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.AddToCartAsync("cookie-existing-wh", product.Id, 1);

        var orders = BuildOrderService(scope, warehouseId: 1);
        var (order, error) = await orders.PlaceOrderAsync(
            "it-existing-wh-user", "cookie-existing-wh", Checkout());

        Assert.True(error is null, error);
        Assert.NotNull(order);
    }

    /// <summary>
    /// چکِ موقع استارتاپ (هشدار در Program.cs) نباید برنامه را کرش کند — در پروداکشن
    /// تنظیمات اشتباه یعنی هشدار در لاگ، نه سایت پایین.
    /// </summary>
    [SkippableFact]
    public async Task App_WithMissingStoreWarehouse_StillBoots()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Default", _db.ConnectionString);
                builder.UseSetting("Store:WarehouseId", "987654");
            });

        _ = factory.Services; // بالا آمدن برنامه — بدون استثنا
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/login");

        Assert.True(response.IsSuccessStatusCode, $"status={(int)response.StatusCode}");
    }
}
