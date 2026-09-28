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
    private readonly Mock<IWarehouseRepository> _warehouses = new();
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

        // ثبت سفارش اول انبارِ تنظیم‌شده («Store:WarehouseId»، پیش‌فرض ۱) را چک می‌کند؛
        // بدون این ست، همهٔ تست‌های مسیر خرید با پیام «انبار تعریف نشده» رد می‌شدند
        _unitOfWork.Setup(u => u.Warehouses).Returns(_warehouses.Object);
        _warehouses.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Warehouse("انبار فروشگاه"));

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
        _stockLevels.Setup(r => r.GetAvailableForSaleAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _stockLevels.Setup(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);
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
        _stockLevels.Setup(r => r.GetAvailableForSaleAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _stockLevels.Setup(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);
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

    // ---------------- رزرو موجودی ----------------

    [Fact]
    public async Task PlaceOrderAsync_ReservesStock_Atomically()
    {
        var product = new Product("SKU-RES", "کالای رزرو", 100m, 50m);
        product.SetStoreDetails(true, "kala-res", null);

        var cart = new Cart { CookieId = "cookie-res" };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 2 });

        _carts.Setup(r => r.GetByCookieIdAsync("cookie-res")).ReturnsAsync(cart);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<Product> { product });
        _stockLevels.Setup(r => r.GetAvailableForSaleAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _stockLevels.Setup(r => r.TryReserveAsync(product.Id, It.IsAny<int>(), 2)).ReturnsAsync(true);
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        var (_, error) = await _sut.PlaceOrderAsync("user-2", "cookie-res", Checkout());

        Assert.Null(error);
        // رزرو حتماً باید انجام شود، وگرنه دو خریدار می‌توانند آخرین موجودی را بخرند
        _stockLevels.Verify(r => r.TryReserveAsync(product.Id, 1, 2), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_WhenReservationFails_ReleasesAlreadyReservedItems()
    {
        // یک کالا، دو سطر سبد: هر دو سطر موجودی کافی دارند (10 در برابر 1)
        // ولی رزروِ سطر اول موفق و رزروِ سطر دوم ناموفق می‌شود (خریدار دیگری زودتر برداشته)
        var product = new Product("SKU-A", "کالای اول", 100m, 50m);
        product.SetStoreDetails(true, "kala-a", null);

        var cart = new Cart { CookieId = "cookie-res-2" };
        cart.Items.Add(new CartItem { Id = 1, ProductId = product.Id, Quantity = 1 });
        cart.Items.Add(new CartItem { Id = 2, ProductId = product.Id, Quantity = 1 });

        _carts.Setup(r => r.GetByCookieIdAsync("cookie-res-2")).ReturnsAsync(cart);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<Product> { product });
        _stockLevels.Setup(r => r.GetAvailableForSaleAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        var reserveCall = 0;
        _stockLevels.Setup(r => r.TryReserveAsync(product.Id, It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(() =>
            {
                reserveCall++;
                return reserveCall == 1; // اولین رزرو موفق، دومی ناموفق
            });

        var (_, error) = await _sut.PlaceOrderAsync("user-3", "cookie-res-2", Checkout());

        Assert.NotNull(error);
        // رزروِ قلم اول باید فوراً آزاد شود تا موجودی برای دیگران بلااستفاده نماند
        _stockLevels.Verify(r => r.ReleaseReservationAsync(product.Id, 1, 1), Times.Once);
        _orders.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
    }

    // ---------------- F: ردِ سفارش بعد از رزرو باید رزرو را آزاد کند ----------------

    /// <summary>
    /// رگرسیون: سقف «مصرف هر مشتری» پر شده و سفارش رد می‌شود. رزروِ انجام‌شده در
    /// <see cref="IStockLevelRepository.TryReserveAsync"/> باید آزاد شود، وگرنه موجودی
    /// آن کالا برای همیشه از فروشگاه کنار گذاشته می‌شود.
    /// </summary>
    [Fact]
    public async Task PlaceOrderAsync_WhenDiscountPerCustomerLimitReached_ReleasesReservation()
    {
        var product = new Product("SKU-LIM", "کالای محدود", 100m, 50m);
        product.SetStoreDetails(true, "kala-lim", null);

        var cart = new Cart { CookieId = "cookie-lim" };
        cart.Items.Add(new CartItem { Id = 1, ProductId = product.Id, Quantity = 2 });

        _carts.Setup(r => r.GetByCookieIdAsync("cookie-lim")).ReturnsAsync(cart);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<Product> { product });
        _stockLevels.Setup(r => r.GetAvailableForSaleAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [product.Id] = 10 });
        _stockLevels.Setup(r => r.TryReserveAsync(product.Id, It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);

        // کد تخفیف با سقف مصرف هر مشتری که قبلاً پر شده
        var code = new DiscountCode { Code = "SAVE20", Value = 20m, MaxUsagePerCustomer = 1 };
        _discountCodes.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(code);
        _orders.Setup(r => r.CountUserDiscountUsagesAsync("user-lim", "SAVE20")).ReturnsAsync(1);

        // سبد به کد تخفیف وصل است
        typeof(Cart).GetProperty(nameof(Cart.DiscountCodeId))!.SetValue(cart, 5);

        var (_, error) = await _sut.PlaceOrderAsync("user-lim", "cookie-lim", Checkout());

        Assert.NotNull(error);
        // رزرو باید آزاد شده باشد — این همان چیزی است که قبلاً از قلم افتاده بود
        _stockLevels.Verify(r => r.ReleaseReservationAsync(product.Id, 1, 2), Times.Once);
        _orders.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
    }

    // ---------------- بازگرداندن رزرو وقتی فاکتور صادر نمی‌شود ----------------

    private readonly Mock<ICustomerRepository> _customers = new();

    /// <summary>
    /// سفارش پرداخت‌نشده با یک قلم (کالای ۳، تعداد ۵). رزرو در همان PlaceOrder گرفته می‌شود؛
    /// در مسیر تسویه، رزرو «پیش» از فاکتور آزاد می‌شود (قید CK_StockLevels_ReservedValid
    /// در دیتابیس اجازهٔ ترتیب دیگر را نمی‌دهد) — پس اگر فاکتور بسته نشود باید برگردد.
    /// </summary>
    private Order PendingOrder()
    {
        var order = new Order
        {
            Id = 9,
            Status = OrderStatus.PendingPayment,
            OrderNumber = "1404-0001",
            CustomerName = "خریدار تست",
            CustomerPhone = "09121239002"
        };
        order.Items.Add(new OrderItem { ProductId = 3, Quantity = 5, UnitPrice = 100m, ProductName = "کالای تست" });

        _orders.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(order);
        _unitOfWork.Setup(u => u.Customers).Returns(_customers.Object);
        _customers.Setup(r => r.GetByPhoneAsync(It.IsAny<string>())).ReturnsAsync((Customer?)null);
        _sales.Setup(s => s.CreateCustomerAsync(It.IsAny<CreateCustomerDto>()))
            .ReturnsAsync(new CustomerDto(11, "خریدار تست", "09121239002", null));
        _sales.Setup(s => s.CreateDraftInvoiceAsync(It.IsAny<CreateSalesInvoiceDto>(), "store")).ReturnsAsync(77);
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store")).Returns(Task.CompletedTask);
        _sales.Setup(s => s.CancelInvoiceAsync(77, "store")).Returns(Task.CompletedTask);

        return order;
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvoiceConfirmFails_RestoresReservation_AndKeepsOrderPending()
    {
        var order = PendingOrder();
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store"))
            .ThrowsAsync(new Dashboard.Domain.Exceptions.BusinessRuleException("موجودی کافی نیست."));
        _stockLevels.Setup(r => r.TryReserveAsync(3, 1, 5m)).ReturnsAsync(true);

        await Assert.ThrowsAsync<Dashboard.Domain.Exceptions.BusinessRuleException>(
            () => _sut.UpdateStatusAsync(9, OrderStatus.Paid, null, "تسویه حضوری"));

        _stockLevels.Verify(r => r.TryReserveAsync(3, 1, 5m), Times.Once);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Null(order.SalesInvoiceId);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvoiceFails_AndStockIsGone_NotifiesAdmin()
    {
        var order = PendingOrder();
        var notifications = new Mock<INotificationService>();
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store"))
            .ThrowsAsync(new Dashboard.Domain.Exceptions.BusinessRuleException("موجودی کافی نیست."));
        _stockLevels.Setup(r => r.TryReserveAsync(3, 1, 5m)).ReturnsAsync(false);

        // OrderService با Mock.Of<INotificationService> ساخته شده؛ برای بررسی اعلان،
        // یک SUT با نوتیفیکیشن mock می‌سازیم
        var sut = new OrderService(_unitOfWork.Object, _sales.Object, Mock.Of<IOutboxService>(),
            notifications.Object, Options.Create(new StoreOptions()), Mock.Of<ITreasuryService>(),
            Mock.Of<ILogger<OrderService>>());

        await Assert.ThrowsAsync<Dashboard.Domain.Exceptions.BusinessRuleException>(
            () => sut.UpdateStatusAsync(9, OrderStatus.Paid, null, "تسویه حضوری"));

        notifications.Verify(n => n.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.Admin,
            It.Is<string>(s => s.Contains("1404-0001")), It.IsAny<string>(),
            It.IsAny<NotificationType>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvoiceSucceeds_DoesNotReReserve()
    {
        PendingOrder();
        _stockLevels.Setup(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);

        await _sut.UpdateStatusAsync(9, OrderStatus.Paid, null, "تسویه حضوری");

        // فاکتور بسته شد ⇒ رزرو باید آزاد بماند (کسر واقعی در تأیید فاکتور انجام شده)
        _stockLevels.Verify(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task MarkPaidAsync_WhenInvoiceHitsTechnicalError_RestoresReservation()
    {
        var order = PendingOrder();
        _orders.Setup(r => r.TryClaimForPaymentAsync(9)).ReturnsAsync(true);
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store"))
            .ThrowsAsync(new Dashboard.Domain.Exceptions.DataIntegrityException("شماره تکراری"));
        _stockLevels.Setup(r => r.TryReserveAsync(3, 1, 5m)).ReturnsAsync(true);

        var (success, error) = await _sut.MarkPaidAsync(9, "auth-1", "ref-1");

        Assert.False(success);
        Assert.NotNull(error);
        // سفارش Paid و بی‌فاکتور می‌ماند (صف پشتیبانی) ⇒ موجودی هم باید برایش قفل شود
        _stockLevels.Verify(r => r.TryReserveAsync(3, 1, 5m), Times.Once);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task MarkPaidAsync_WhenSystemErrorInInvoice_RestoresReservation()
    {
        var order = PendingOrder();
        _orders.Setup(r => r.TryClaimForPaymentAsync(9)).ReturnsAsync(true);
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store"))
            .ThrowsAsync(new InvalidOperationException("temp glitch"));
        _stockLevels.Setup(r => r.TryReserveAsync(3, 1, 5m)).ReturnsAsync(true);

        var (success, _) = await _sut.MarkPaidAsync(9, "auth-1", "ref-1");

        Assert.False(success);
        _stockLevels.Verify(r => r.TryReserveAsync(3, 1, 5m), Times.Once);
    }

    [Fact]
    public async Task MarkPaidAsync_WhenStockRanOut_CancelsOrder_AndLeavesReservationReleased()
    {
        PendingOrder();
        _orders.Setup(r => r.TryClaimForPaymentAsync(9)).ReturnsAsync(true);
        _sales.Setup(s => s.ConfirmInvoiceAsync(77, "store"))
            .ThrowsAsync(new Dashboard.Domain.Exceptions.BusinessRuleException("موجودی کافی نیست."));
        _stockLevels.Setup(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);

        var (success, _) = await _sut.MarkPaidAsync(9, "auth-1", "ref-1");

        Assert.False(success);
        // سفارش لغو شده ⇒ رزرو باید آزاد بماند تا کالا دوباره فروش برود
        _stockLevels.Verify(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
    }
}