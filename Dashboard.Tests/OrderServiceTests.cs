using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dashboard.Tests;

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICartRepository> _carts = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockLevelRepository> _stockLevels = new();
    private readonly Mock<IDiscountCodeRepository> _discountCodes = new();
    private readonly Mock<ISalesService> _sales = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _unitOfWork.Setup(u => u.Orders).Returns(_orders.Object);
        _unitOfWork.Setup(u => u.Carts).Returns(_carts.Object);
        _unitOfWork.Setup(u => u.Products).Returns(_products.Object);
        _unitOfWork.Setup(u => u.StockLevels).Returns(_stockLevels.Object);
        _unitOfWork.Setup(u => u.DiscountCodes).Returns(_discountCodes.Object);
        _unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        _sut = new OrderService(
            _unitOfWork.Object,
            _sales.Object,
            Mock.Of<IOutboxService>(),
            Mock.Of<INotificationService>(),
            Options.Create(new StoreOptions()),
            Mock.Of<ITreasuryService>(),
            Mock.Of<ILogger<OrderService>>());
    }

    private static CheckoutDto Checkout() => new()
    {
        CustomerName = "کاربر تست",
        CustomerPhone = "09123456789",
        Province = "تهران",
        City = "تهران",
        AddressLine = "خیابان تست، پلاک ۱",
        ShippingMethod = ShippingMethod.Post
    };

    // ---------------- E: لغو تکراری سفارشِ لغوشده نباید فاکتور را دوباره لغو کند ----------------

    [Fact]
    public async Task UpdateStatusAsync_WhenOrderAlreadyCanceled_DoesNotCancelInvoiceAgain()
    {
        var order = new Order { Id = 7, Status = OrderStatus.Canceled, SalesInvoiceId = 55 };
        _orders.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(order);

        // ذخیرهٔ مجدد روی سفارش لغوشده (مثلاً برای افزودن یادداشت) نباید استثنا بدهد
        await _sut.UpdateStatusAsync(7, OrderStatus.Canceled, null, "یادداشت ادمین");

        _sales.Verify(s => s.CancelInvoiceAsync(55, "admin"), Times.Never);
        _orders.Verify(r => r.UpdateAsync(order), Times.Once);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal("یادداشت ادمین", order.AdminNote);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenTransitioningToCanceled_CancelsInvoiceOnce()
    {
        var order = new Order { Id = 7, Status = OrderStatus.Paid, SalesInvoiceId = 55 };
        _orders.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(order);

        await _sut.UpdateStatusAsync(7, OrderStatus.Canceled, null, null);

        _sales.Verify(s => s.CancelInvoiceAsync(55, "admin"), Times.Once);
        Assert.Equal(OrderStatus.Canceled, order.Status);
    }

    // ---------------- C: سقف «مصرف هر مشتری» برای کد تخفیف ----------------

    [Fact]
    public async Task PlaceOrderAsync_WhenPerCustomerDiscountLimitReached_ReturnsError()
    {
        var product = new Product("SKU-TEST", "کالای تست", 100m, 50m);
        product.SetStoreDetails(true, "kala-tes", null);

        var cart = new Cart { CookieId = "cookie-1", DiscountCodeId = 3 };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 2 });

        _carts.Setup(r => r.GetByCookieIdAsync("cookie-1")).ReturnsAsync(cart);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<Product> { product });
        _stockLevels.Setup(r => r.GetWarehouseStockAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _discountCodes.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new DiscountCode
        {
            Id = 3,
            Code = "SAVE10",
            Type = DiscountType.Percentage,
            Value = 10,
            IsActive = true,
            MaxUsagePerCustomer = 1
        });
        // این کاربر قبلاً یک بار از همین کد استفاده کرده است
        _orders.Setup(r => r.CountUserDiscountUsagesAsync("user-1", "SAVE10")).ReturnsAsync(1);

        var (_, error) = await _sut.PlaceOrderAsync("user-1", "cookie-1", Checkout());

        Assert.NotNull(error);
        Assert.Contains("سقف مصرف", error);
        _orders.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task PlaceOrderAsync_WhenPerCustomerLimitNotReached_AppliesDiscountAndShipping()
    {
        var product = new Product("SKU-TEST", "کالای تست", 100m, 50m);
        product.SetStoreDetails(true, "kala-tes", null);

        var cart = new Cart { CookieId = "cookie-1", DiscountCodeId = 3 };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 2 });

        _carts.Setup(r => r.GetByCookieIdAsync("cookie-1")).ReturnsAsync(cart);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<Product> { product });
        _stockLevels.Setup(r => r.GetWarehouseStockAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _discountCodes.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new DiscountCode
        {
            Id = 3,
            Code = "SAVE10",
            Type = DiscountType.Percentage,
            Value = 10,
            IsActive = true,
            MaxUsagePerCustomer = 2
        });
        _orders.Setup(r => r.CountUserDiscountUsagesAsync("user-1", "SAVE10")).ReturnsAsync(1);

        Order? savedOrder = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        var (_, error) = await _sut.PlaceOrderAsync("user-1", "cookie-1", Checkout());

        Assert.Null(error);
        Assert.NotNull(savedOrder);
        // 10٪ از جمع ۲۰۰
        Assert.Equal(20m, savedOrder!.DiscountAmount);
        // حمل‌ونقل پست از تنظیمات پیش‌فرض (۸۰٬۰۰۰)
        Assert.Equal(80_000m, savedOrder.ShippingCost);
        // Total = جمع − تخفیف + حمل‌ونقل — همان مبلغی که به درگاه می‌رود
        Assert.Equal(200m - 20m + 80_000m, savedOrder.Total);
        Assert.Equal("SAVE10", savedOrder.DiscountCodeText);
    }
}