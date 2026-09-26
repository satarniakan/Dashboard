// Dashboard.Application/Services/OrderService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IOrderService
{
    /// <summary>هزینه‌ی هر روش ارسال (تومان) از تنظیمات فروشگاه — برای نمایش در صفحه‌ی تسویه</summary>
    decimal GetShippingCost(ShippingMethod method);

    /// <summary>ثبت سفارش از روی سبد؛ خروجی سفارش PendingPayment است و باید به درگاه پرداخت رفت</summary>
    Task<(OrderDto Order, string? Error)> PlaceOrderAsync(string userId, string cookieId, CheckoutDto dto);

    /// <summary>
    /// پرداخت موفق: کسر اتمیک انبار، ساخت فاکتور فروش و سند حسابداری از مسیر SalesService،
    /// مصرف کد تخفیف، خالی‌کردن سبد. اگر موجودی کافی نباشد سفارش لغو و خطا برگردانده می‌شود.
    /// </summary>
    Task<(bool Success, string? Error)> MarkPaidAsync(int orderId, string authority, string? refId);

    Task<(bool Success, string? Error)> MarkPaymentFailedAsync(string authority, string? error);

    /// <summary>ثبت رکورد پرداخت (Initiated) بعد از پاسخ موفق درگاه</summary>
    Task AttachPaymentAsync(int orderId, string gateway, decimal amount, string authority);

    /// <summary>یافتن سفارش با Authority درگاه — برای callback</summary>
    Task<OrderDto?> GetByAuthorityAsync(string authority);

    Task<OrderDto?> GetForUserAsync(int orderId, string userId);

    Task<IEnumerable<OrderSummaryDto>> GetForUserAsync(string userId);

    // --- ادمین ---
    Task<(IEnumerable<OrderSummaryDto> Items, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, OrderStatus? status = null, string? search = null);

    Task<OrderDto?> GetForAdminAsync(int orderId);

    Task UpdateStatusAsync(int orderId, OrderStatus status, string? trackingCode, string? adminNote);

    /// <summary>انقضای سفارش‌های پرداخت‌نشده‌ی قدیمی — BackgroundService</summary>
    Task<int> ExpireStalePendingOrdersAsync(TimeSpan maxAge);
}

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISalesService _salesService;
    private readonly IOutboxService _smsQueue;
    private readonly INotificationService _notifications;
    private readonly StoreOptions _store;

    public OrderService(IUnitOfWork unitOfWork, ISalesService salesService, IOutboxService smsQueue,
        INotificationService notifications, Microsoft.Extensions.Options.IOptions<StoreOptions> storeOptions)
    {
        _unitOfWork = unitOfWork;
        _salesService = salesService;
        _smsQueue = smsQueue;
        _notifications = notifications;
        _store = storeOptions.Value;
    }

    /// <summary>هزینه‌ی هر روش ارسال از تنظیمات فروشگاه</summary>
    public decimal GetShippingCost(ShippingMethod method) => _store.GetShippingCost(method);

    public async Task<(OrderDto Order, string? Error)> PlaceOrderAsync(string userId, string cookieId, CheckoutDto dto)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.Items.Count == 0)
            return (default!, "سبد خرید شما خالی است.");

        var products = (await _unitOfWork.Products.GetByIdsAsync(cart.Items.Select(i => i.ProductId).ToList()))
            .Where(p => p.IsPublished)
            .ToDictionary(p => p.Id);

        if (products.Count == 0)
            return (default!, "کالاهای سبد شما دیگر قابل خرید نیستند.");

        // موجودی کافی؟ (کسر واقعی هنگام تأیید پرداخت با RowVersion انجام می‌شود)
        var stock = await _unitOfWork.StockLevels.GetTotalStockAsync(products.Keys.ToList());
        foreach (var item in cart.Items)
        {
            var available = stock.TryGetValue(item.ProductId, out var s) ? s : 0;
            if (item.Quantity > available)
            {
                var name = products.GetValueOrDefault(item.ProductId)?.Name ?? "کالا";
                return (default!, $"موجودی «{name}» کافی نیست (حداکثر {available:0.##} {products.GetValueOrDefault(item.ProductId)?.Unit}). سبد را به‌روز کنید.");
            }
        }

        var subtotal = cart.Items.Sum(i => i.Quantity * (products.GetValueOrDefault(i.ProductId)?.Price ?? 0));

        // کد تخفیف — اعتبارسنجی مجدد هنگام ثبت
        decimal discountAmount = 0m;
        string? discountCodeText = null;
        DiscountCode? appliedCode = null;
        if (cart.DiscountCodeId is not null)
        {
            appliedCode = await _unitOfWork.DiscountCodes.GetByIdAsync(cart.DiscountCodeId.Value);
            if (appliedCode is not null && appliedCode.IsValidNow(DateTime.UtcNow)
                && (appliedCode.MaxUsageCount is null || appliedCode.UsageCount < appliedCode.MaxUsageCount)
                && appliedCode.CalculateDiscount(subtotal) > 0)
            {
                discountAmount = appliedCode.CalculateDiscount(subtotal);
                discountCodeText = appliedCode.Code;
            }
        }

        var shippingCost = GetShippingCost(dto.ShippingMethod);

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            CustomerName = dto.CustomerName.Trim(),
            CustomerPhone = dto.CustomerPhone.Trim(),
            Province = dto.Province.Trim(),
            City = dto.City.Trim(),
            AddressLine = dto.AddressLine.Trim(),
            PostalCode = dto.PostalCode,
            ShippingMethod = dto.ShippingMethod,
            ShippingCost = shippingCost,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            DiscountCodeText = discountCodeText,
            Status = OrderStatus.PendingPayment,
        };

        foreach (var item in cart.Items)
        {
            var p = products[item.ProductId];
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = p.Name,
                UnitPrice = p.Price,
                Quantity = item.Quantity
            });
        }

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.CompleteAsync();

        return (ToDto(order), null);
    }

    public async Task<(bool Success, string? Error)> MarkPaidAsync(int orderId, string authority, string? refId)
    {
        var order = await _unitOfWork.Orders.GetByIdWithDetailsAsync(orderId);
        if (order is null)
            return (false, "سفارش یافت نشد.");

        if (order.Status != OrderStatus.PendingPayment)
            return (true, null); // قبلاً پردازش شده (idempotent)

        // ثبت پرداخت موفق
        var payment = order.Payments.FirstOrDefault(p => p.Authority == authority);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Success;
            payment.RefId = refId;
            payment.VerifiedAt = DateTime.UtcNow;
        }

        try
        {
            // ۱. مشتری: با شماره موبایل پیدا یا ساخته می‌شود
            var existingCustomer = await _unitOfWork.Customers.GetByPhoneAsync(order.CustomerPhone);
            var customerId = existingCustomer?.Id
                ?? (await _salesService.CreateCustomerAsync(new CreateCustomerDto
                {
                    Name = order.CustomerName,
                    Phone = order.CustomerPhone,
                    Address = $"{order.Province}، {order.City}، {order.AddressLine}"
                })).Id;

            // ۲. فاکتور پیش‌نویس از اقلام سفارش
            var invoiceId = await _salesService.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId,
                WarehouseId = _store.WarehouseId,
                DiscountAmount = order.DiscountAmount,
                Notes = $"سفارش آنلاین {order.OrderNumber}" + (refId is null ? "" : $" — شماره پیگیری: {refId}"),
                Items = order.Items.Select(i => new SalesInvoiceItemInput
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            }, "store");

            // ۳. تأیید فاکتور: کسر اتمیک انبار + سند حسابداری خودکار
            await _salesService.ConfirmInvoiceAsync(invoiceId, "store");

            order.SalesInvoiceId = invoiceId;
            order.Status = OrderStatus.Paid;
            order.PaidAt = DateTime.UtcNow;

            // ۴. مصرف کد تخفیف
            if (order.DiscountCodeText is not null)
            {
                var code = await _unitOfWork.DiscountCodes.GetByCodeAsync(order.DiscountCodeText);
                if (code is not null)
                {
                    code.UsageCount++;
                    await _unitOfWork.DiscountCodes.UpdateAsync(code);
                }
            }

            await _unitOfWork.CompleteAsync();

            // ۵. پاک‌کردن سبد به عهده‌ی callback است (کوکی سبد آنجا در دسترس است)؛
            // اینجا اطلاع‌رسانی پیامکی ثبت می‌شود
            await _smsQueue.QueueSmsAsync(order.CustomerPhone,
                $"سفارش {order.OrderNumber} شما با موفقیت ثبت شد. مبلغ: {order.Total:0} تومان");

            await _notifications.NotifyAsync(order.UserId,
                $"سفارش {order.OrderNumber} ثبت شد",
                $"مبلغ {order.Total:0} تومان — در حال پردازش",
                NotificationType.Order, $"/shop/orders/{order.Id}");

            // اعلان به ادمین‌ها و انباردار: سفارش جدید نیازمند پردازش
            var staffBody = $"مشتری: {order.CustomerName} — مبلغ {order.Total:0} تومان";
            await _notifications.NotifyRoleAsync(Roles.Admin, $"سفارش جدید {order.OrderNumber}",
                staffBody, NotificationType.Order, $"/admin/orders/{order.Id}");
            await _notifications.NotifyRoleAsync(Roles.WarehouseUser, $"سفارش جدید {order.OrderNumber}",
                staffBody, NotificationType.Order, $"/admin/orders/{order.Id}");

            return (true, null);
        }
        catch (BusinessRuleException ex)
        {
            // مهم‌ترین حالت: موجودی بین پرداخت و تأیید تمام شده — سفارش لغو می‌شود
            // و بازگشت وجه باید دستی از پنل درگاه انجام شود (توجه ادمین لازم است)
            order.Status = OrderStatus.Canceled;
            order.AdminNote = $"پرداخت موفق اما ثبت سفارش ناموفق: {ex.Message}";
            await _unitOfWork.CompleteAsync();
            return (false, $"پرداخت انجام شد اما ثبت سفارش ناموفق بود: {ex.Message}");
        }
        catch (Exception ex)
        {
            // خطای سیستمی موقتی: سفارش پرداخت‌نشده نمی‌ماند ولی لغو هم نمی‌شود؛
            // با یادداشت برای ادمین می‌ماند تا بررسی/تلاش مجدد دستی انجام شود
            order.AdminNote = $"پرداخت موفق؛ خطای سیستمی در صدور فاکتور: {ex.Message}";
            await _unitOfWork.CompleteAsync();
            return (false, $"پرداخت انجام شد اما صدور فاکتور با خطا مواجه شد؛ سفارش برای بررسی ادمین علامت خورد.");
        }
    }

    public async Task AttachPaymentAsync(int orderId, string gateway, decimal amount, string authority)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
                    ?? throw new NotFoundException("سفارش", orderId);

        order.Payments.Add(new Payment
        {
            Gateway = gateway,
            Amount = amount,
            Authority = authority,
            Status = PaymentStatus.Initiated
        });
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<OrderDto?> GetByAuthorityAsync(string authority)
    {
        var order = await _unitOfWork.Orders.GetByAuthorityAsync(authority);
        return order is null ? null : ToDto(order);
    }

    public async Task<(bool Success, string? Error)> MarkPaymentFailedAsync(string authority, string? error)
    {
        var order = await _unitOfWork.Orders.GetByAuthorityAsync(authority);
        if (order is null)
            return (false, "سفارش یافت نشد.");

        if (order.Status != OrderStatus.PendingPayment)
            return (true, null);

        var payment = order.Payments.FirstOrDefault(p => p.Authority == authority);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Failed;
            payment.VerifiedAt = DateTime.UtcNow;
        }

        order.Status = OrderStatus.Canceled;
        order.AdminNote = $"پرداخت ناموفق: {error}";
        await _unitOfWork.CompleteAsync();
        return (true, null);
    }

    public async Task<OrderDto?> GetForUserAsync(int orderId, string userId)
    {
        var order = await _unitOfWork.Orders.GetByIdWithDetailsAsync(orderId);
        return order is null || order.UserId != userId ? null : ToDto(order);
    }

    public async Task<IEnumerable<OrderSummaryDto>> GetForUserAsync(string userId) =>
        (await _unitOfWork.Orders.GetByUserAsync(userId)).Select(ToSummary);

    public async Task<(IEnumerable<OrderSummaryDto> Items, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, OrderStatus? status = null, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        var (items, total) = await _unitOfWork.Orders.GetPagedAsync(page, pageSize, status, search);
        return (items.Select(ToSummary).ToList(), total);
    }

    public async Task<OrderDto?> GetForAdminAsync(int orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdWithDetailsAsync(orderId);
        return order is null ? null : ToDto(order);
    }

    public async Task UpdateStatusAsync(int orderId, OrderStatus status, string? trackingCode, string? adminNote)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
                    ?? throw new NotFoundException("سفارش", orderId);

        order.Status = status;
        order.TrackingCode = trackingCode?.Trim();
        order.AdminNote = adminNote?.Trim();
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<int> ExpireStalePendingOrdersAsync(TimeSpan maxAge)
    {
        var stale = await _unitOfWork.Orders.GetStalePendingPaymentAsync(maxAge);
        foreach (var order in stale)
        {
            order.Status = OrderStatus.Canceled;
            order.AdminNote = "انقضای سفارش پرداخت‌نشده";
            await _unitOfWork.Orders.UpdateAsync(order);

            await _notifications.NotifyAsync(order.UserId,
                $"سفارش {order.OrderNumber} لغو شد",
                "به دلیل عدم پرداخت در مهلت مقرر لغو شد؛ در صورت تمایل می‌توانید دوباره خرید کنید.",
                NotificationType.Order, $"/shop/orders/{order.Id}");
        }
        if (stale.Count > 0)
            await _unitOfWork.CompleteAsync();
        return stale.Count;
    }

    private static string GenerateOrderNumber()
    {
        // تاریخ شمسی واقعی به وقت تهران (UTC+3:30) — یکتایی هم با پسوند Guid تضمین می‌شود
        var tehranNow = DateTime.UtcNow.AddHours(3.5);
        var pc = new System.Globalization.PersianCalendar();
        var datePart = $"{pc.GetYear(tehranNow):0000}{pc.GetMonth(tehranNow):00}{pc.GetDayOfMonth(tehranNow):00}";
        return $"ORD-{datePart}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
    }

    private static string StatusText(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت",
        OrderStatus.Paid => "پرداخت شده",
        OrderStatus.Processing => "در حال پردازش",
        OrderStatus.Shipped => "ارسال شده",
        OrderStatus.Delivered => "تحویل شده",
        OrderStatus.Canceled => "لغو شده",
        _ => status.ToString()
    };

    private static string ShippingText(ShippingMethod m) => m switch
    {
        ShippingMethod.Post => "پست پیشتاز",
        ShippingMethod.Courier => "پیک",
        ShippingMethod.InPerson => "تحویل حضوری",
        _ => m.ToString()
    };

    private static OrderSummaryDto ToSummary(Order o) =>
        new(o.Id, o.OrderNumber, o.Status, StatusText(o.Status), o.CustomerName, o.Total, o.CreatedAt);

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.OrderNumber, o.Status, StatusText(o.Status),
        o.CustomerName, o.CustomerPhone, o.Province, o.City, o.AddressLine, o.PostalCode,
        ShippingText(o.ShippingMethod),
        o.Subtotal, o.DiscountAmount, o.DiscountCodeText, o.ShippingCost, o.Total,
        o.TrackingCode, o.AdminNote, o.CreatedAt, o.SalesInvoiceId,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal, i.Product?.ImageUrl)).ToList());
}
