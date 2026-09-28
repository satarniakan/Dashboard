using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// مسیر تسویهٔ حضوری (UpdateStatusAsync → Paid): برخلاف مسیر درگاه، هیچ تست یکپارچگی
/// نداشت. این تست «انبار و فاکتور و رزرو بعد از تسویه دستی» را قفل می‌کند — و همان
/// چیزی را ثابت می‌کند که قید CK_StockLevels_ReservedValid تحمیل می‌کند: رزرو پیش از
/// کسر موجودی آزاد می‌شود، پس هر دو در پایان صفرند.
/// </summary>
[Collection("Database")]
public class StoreManualSettlementTests
{
    private readonly TestDatabaseFixture _db;

    public StoreManualSettlementTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task ManualPayment_InvoicePosted_AndReservationAndStockBothCleared()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        const int warehouse = 1;

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync("cookie-rr-2", product.Id, 5);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-rr-user-2", "cookie-rr-2", new CheckoutDto
            {
                CustomerName = "خریدار تست",
                CustomerPhone = "09121239002",
                Province = "تهران",
                City = "تهران",
                AddressLine = "آدرس تست",
                ShippingMethod = ShippingMethod.Post
            });

            Assert.Null(error);
            orderId = order.Id;
        }

        // پیش از تسویه: موجودی فیزیکی سر جایش ولی قفلِ رزرو
        using (var before = _db.CreateScope())
        {
            var context = before.ServiceProvider.GetRequiredService<AppDbContext>();
            var level = await context.StockLevels
                .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse);
            Assert.Equal(5m, level.QuantityOnHand);
            Assert.Equal(5m, level.ReservedQuantity);
        }

        using (var scope = _db.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            await orders.UpdateStatusAsync(orderId, OrderStatus.Paid, null, "تسویه حضوری");
        }

        using (var verify = _db.CreateScope())
        {
            var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

            var level = await context.StockLevels
                .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse);

            // فاکتور صادر شد ⇒ کسر واقعی انجام شده و رزرو هم آزاد شده است
            Assert.NotNull(await context.Orders.Where(o => o.Id == orderId && o.SalesInvoiceId != null)
                .Select(o => o.SalesInvoiceId).FirstOrDefaultAsync());
            Assert.Equal(0m, level.ReservedQuantity);
            Assert.Equal(0m, level.QuantityOnHand);
        }
    }
}
