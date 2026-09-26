// Dashboard.Application/Services/OrderService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;

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
    private readonly ITreasuryService _treasury;
    private readonly Microsoft.Extensions.Logging.ILogger<OrderService> _logger;
    private readonly StoreOptions _store;

    public OrderService(IUnitOfWork unitOfWork, ISalesService salesService, IOutboxService smsQueue,
        INotificationService notifications, Microsoft.Extensions.Options.IOptions<StoreOptions> storeOptions,
        ITreasuryService treasury, Microsoft.Extensions.Logging.ILogger<OrderService> logger)
    {
        _unitOfWork = unitOfWork;
        _salesService = salesService;
        _smsQueue = smsQueue;
        _notifications = notifications;
        _treasury = treasury;
        _logger = logger;
        _store = storeOptions.Value;
    }

    /// <summary>هزینه‌ی هر روش ارسال از تنظیمات فروشگاه</summary>
    public decimal GetShippingCost(ShippingMethod method) => _store.GetShippingCost(method);

    public async Task<(OrderDto Order, string? Error)> PlaceOrderAsync(string userId, string cookieId, CheckoutDto dto)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.Items.Count == 0)
            return (default!, "سبد خرید شما خالی است.");

        // همهٔ کالاهای سبد خوانده می‌شوند تا برای کالای حذف‌شده/مخفی‌شده پیام دقیق داده شود
        // (فقط کالاهای منتشرشده قابل خریدند و جمع زیرمجموعه باید با سبد یکی باشد)
        var allProducts = (await _unitOfWork.Products.GetByIdsAsync(cart.Items.Select(i => i.ProductId).ToList()))
            .ToDictionary(p => p.Id);
        var products = allProducts.Values.Where(p => p.IsPublished).ToDictionary(p => p.Id);

        var unavailableItem = cart.Items.FirstOrDefault(i =>
            !allProducts.TryGetValue(i.ProductId, out var p) || !p.IsPublished);
        if (unavailableItem is not null)
        {
            var name = allProducts.GetValueOrDefault(unavailableItem.ProductId)?.Name;
            return (default!, name is null
                ? "یکی از کالاهای سبد شما دیگر در فروشگاه وجود ندارد؛ لطفاً آن را از سبد حذف کنید."
                : $"کالای «{name}» دیگر برای خرید موجود نیست؛ لطفاً آن را از سبد حذف کنید.");
        }

        if (products.Count == 0)
            return (default!, "کالاهای سبد شما دیگر قابل خرید نیستند.");

        // موجودی کافی؟ فقط انبار فروشگاه — کسر واقعی هنگام تأیید پرداخت از همین انبار
        // با DecreaseWithCheckAsync (اتمیک + RowVersion) انجام می‌شود
        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(products.Keys.ToList(), _store.WarehouseId);
        foreach (var item in cart.Items)
        {
            var available = stock.TryGetValue(item.ProductId, out var s) ? s : 0;
            if (item.Quantity > available)
            {
                var name = products.GetValueOrDefault(item.ProductId)?.Name ?? "کالا";
                return (default!, $"موجودی «{name}» کافی نیست (حداکثر {available:0.##} {products.GetValueOrDefault(item.ProductId)?.Unit}). سبد را به‌روز کنید.");
            }
        }

        // گرد کردن به ۲ رقم اعشار: مبلغ ارسالی به درگاه و مبلغ زمان verify باید
        // دقیقاً یکی باشند (مقدار ذخیره‌شده در DB با دقت decimal(18,2) گرد می‌شود)
        var subtotal = Math.Round(cart.Items.Sum(i => i.Quantity * (products.GetValueOrDefault(i.ProductId)?.Price ?? 0)), 2, MidpointRounding.AwayFromZero);

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
                // سقف «مصرف هر مشتری» — اگر پر شده باشد کد اعمال نمی‌شود و کاربر باید آن را از سبد حذف کند
                if (appliedCode.MaxUsagePerCustomer is int maxPerCustomer && maxPerCustomer > 0)
                {
                    var usedCount = await _unitOfWork.Orders.CountUserDiscountUsagesAsync(userId, appliedCode.Code);
                    if (usedCount >= maxPerCustomer)
                        return (default!,
                            $"سقف مصرف این کد تخفیف برای شما پر شده است ({usedCount} از {maxPerCustomer} بار). کد را از سبد خرید حذف کنید.");
                }

                discountAmount = Math.Round(appliedCode.CalculateDiscount(subtotal), 2, MidpointRounding.AwayFromZero);
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

        // claim اتمیک در دیتابیس: فقط یک درخواست (از بین callbackهای تکراری/موازی)
        // می‌تواند سفارش PendingPayment را به Paid تبدیل کند — جلوی دوبار فروختن را می‌گیرد
        var claimed = order.Status == OrderStatus.PendingPayment
            && await _unitOfWork.Orders.TryClaimForPaymentAsync(orderId);

        if (!claimed)
        {
            var current = await _unitOfWork.Orders.GetStatusAsync(orderId);
            if (current is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
                return (true, null); // قبلاً پردازش شده (idempotent)

            // سفارش لغو/منقضی شده ولی پول در درگاه دریافت شده — ثبت پرداخت و اطلاع به ادمین برای بازگشت وجه
            var failedPayment = order.Payments.FirstOrDefault(p => p.Authority == authority);
            if (failedPayment is not null)
            {
                failedPayment.Status = PaymentStatus.Success;
                failedPayment.RefId = refId;
                failedPayment.VerifiedAt = DateTime.UtcNow;
            }
            order.AdminNote = "پرداخت روی سفارش لغوشده انجام شد — نیازمند بازگشت وجه دستی از پنل درگاه.";
            await _unitOfWork.CompleteAsync();

            await _notifications.NotifyRoleAsync(Roles.Admin, $"پرداخت روی سفارش لغوشده {order.OrderNumber}",
                "مبلغ از مشتری دریافت شد ولی سفارش قبلاً لغو/منقضی شده است. بازگشت وجه از پنل درگاه لازم است.",
                NotificationType.Order, $"/admin/orders/{order.Id}");

            return (false, "سفارش پیش از تکمیل پرداخت لغو شده بود. مبلغ دریافت‌شده باید از سمت درگاه بازگردانده شود؛ لطفاً با پشتیبانی تماس بگیرید.");
        }

        // هم‌زمان‌سازی انتیتی track‌شده با دیتابیس (claim مستقیم در DB انجام شد)
        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;

        // ثبت پرداخت موفق — بلافاصله ذخیره می‌شود تا حتی اگر ادامه شکست خورد،
        // رسید پول از دست نرود
        var payment = order.Payments.FirstOrDefault(p => p.Authority == authority);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Success;
            payment.RefId = refId;
            payment.VerifiedAt = DateTime.UtcNow;
        }
        await _unitOfWork.CompleteAsync();

        int? invoiceId = null;
        try
        {
            // ۱-۳. مشتری (find-or-create) + فاکتور پیش‌نویس + تأیید: کسر اتمیک انبار و سند حسابداری
            invoiceId = await CreateConfirmedInvoiceAsync(order, refId: refId);
            order.SalesInvoiceId = invoiceId;

            // ۴. مصرف اتمیک کد تخفیف (UPDATE مشروط در DB — سقف هرگز رد نمی‌شود)
            if (order.DiscountCodeText is not null)
            {
                var code = await _unitOfWork.DiscountCodes.GetByCodeAsync(order.DiscountCodeText);
                if (code is not null && !await _unitOfWork.DiscountCodes.TryConsumeUsageAsync(code.Id))
                    order.AdminNote = "سقف مصرف کد تخفیف بین ثبت سفارش و پرداخت پر شد؛ تخفیف این سفارش حفظ شد.";
            }

            // ⚠️ ایجاد فاکتور ممکن است به‌دلیل تصادم همزمانی، change tracker را پاک کرده باشد
            // (SalesService.ClearChangeTracker) و این انتیتی را detach کرده باشد؛ بدون Update زیر،
            // SalesInvoiceId/AdminNote بی‌صدا ذخیره نمی‌شدند و بعداً لغو سفارش، فاکتور را برنمی‌گرداند.
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.CompleteAsync();

            // رسید دریافت (بدهکار صندوق/بانک انتخاب‌شده، بستانکار حساب‌های دریافتنی) —
            // فقط اگر «Store:OnlinePaymentFinancialAccountId» تنظیم شده باشد؛ خطا هرگز پرداخت را از کار نمی‌اندازد
            await TryRegisterOnlinePaymentReceiptAsync(order, invoiceId, refId);

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
            // مهم‌ترین حالت: موجودی بین پرداخت و تأیید تمام شده — پیش‌نویس فاکتور لغو،
            // سفارش لغو می‌شود و بازگشت وجه باید دستی از پنل درگاه انجام شود
            if (invoiceId is not null)
            {
                try { await _salesService.CancelInvoiceAsync(invoiceId.Value, "store"); }
                catch { /* لغو پیش‌نویس بهترین‌تلاش است؛ اثر انباری نداشته */ }
            }
            order.Status = OrderStatus.Canceled;
            order.AdminNote = $"پرداخت موفق اما ثبت سفارش ناموفق: {ex.Message} — نیازمند بازگشت وجه.";
            // ممکن است tracker در مسیر تأیید فاکتور پاک شده باشد — دوباره متصل می‌شود تا ذخیره واقعاً انجام شود
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.CompleteAsync();

            await _notifications.NotifyRoleAsync(Roles.Admin, $"پرداخت موفق، ثبت ناموفق — سفارش {order.OrderNumber}",
                $"موجودی کافی نبود و سفارش لغو شد. بازگشت وجه از پنل درگاه لازم است. جزئیات: {ex.Message}",
                NotificationType.Order, $"/admin/orders/{order.Id}");

            return (false, $"پرداخت انجام شد اما ثبت سفارش ناموفق بود: {ex.Message}");
        }
        catch (Exception ex)
        {
            // خطای سیستمی موقتی: سفارش Paid می‌ماند (claim قبلاً commit شده) ولی بدون فاکتور؛
            // با یادداشت برای ادمین علامت می‌خورد تا بررسی/تلاش مجدد دستی انجام شود
            order.AdminNote = $"پرداخت موفق؛ خطای سیستمی در صدور فاکتور: {ex.Message}";
            // ممکن است tracker در مسیر تأیید فاکتور پاک شده باشد — دوباره متصل می‌شود تا یادداشت ذخیره شود
            await _unitOfWork.Orders.UpdateAsync(order);
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

    /// <summary>گذارهای مجاز وضعیت سفارش — Delivered و Canceled پایانی هستند</summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.PendingPayment] = new[] { OrderStatus.Paid, OrderStatus.Canceled },
        [OrderStatus.Paid] = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Canceled },
        [OrderStatus.Processing] = new[] { OrderStatus.Shipped, OrderStatus.Canceled },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Canceled] = Array.Empty<OrderStatus>(),
    };

    public async Task UpdateStatusAsync(int orderId, OrderStatus status, string? trackingCode, string? adminNote)
    {
        var order = await _unitOfWork.Orders.GetByIdWithDetailsAsync(orderId)
                    ?? throw new NotFoundException("سفارش", orderId);

        if (order.Status != status && !AllowedTransitions[order.Status].Contains(status))
            throw new BusinessRuleException(
                $"تغییر وضعیت از «{StatusText(order.Status)}» به «{StatusText(status)}» مجاز نیست.");

        // پرداخت دستی (مثلاً تسویه‌ی حضوری): فاکتور فروش صادر می‌شود تا انبار و حسابداری
        // بدون فاکتور نمانند — در صورت نبود موجودی، خطا و وضعیت عوض نمی‌شود
        if (status == OrderStatus.Paid && order.Status != OrderStatus.Paid)
        {
            order.SalesInvoiceId = await CreateConfirmedInvoiceAsync(order, noteSuffix: " — پرداخت دستی توسط ادمین");
        }

        // لغو سفارشِ دارای فاکتور تأییدشده: برگشت موجودی و سند معکوس حسابداری
        // فقط هنگام گذار واقعی به «لغوشده» — ذخیرهٔ مجدد روی سفارش لغوشده (مثلاً برای افزودن یادداشت)
        // نباید فاکتور قبلاً لغوشده را دوباره لغو کند و خطای «این فاکتور قبلاً لغو شده» بدهد
        if (status == OrderStatus.Canceled && order.Status != OrderStatus.Canceled && order.SalesInvoiceId is not null)
        {
            await _salesService.CancelInvoiceAsync(order.SalesInvoiceId.Value, "admin");
        }

        order.Status = status;
        if (status == OrderStatus.Paid) order.PaidAt ??= DateTime.UtcNow;

        // فقط در صورت ارسال مقدار بازنویسی می‌شوند — یادداشت/کد رهگیری قبلی بی‌قید پاک نمی‌شود
        if (trackingCode is not null) order.TrackingCode = trackingCode.Trim();
        if (adminNote is not null) order.AdminNote = adminNote.Trim();

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.CompleteAsync();
    }

    /// <summary>ساخت مشتری (find-or-create) + فاکتور پیش‌نویس + تأیید (کسر اتمیک انبار و سند حسابداری)</summary>
    private async Task<int> CreateConfirmedInvoiceAsync(Order order, string? noteSuffix = null, string? refId = null)
    {
        var existingCustomer = await _unitOfWork.Customers.GetByPhoneAsync(order.CustomerPhone);
        var customerId = existingCustomer?.Id
            ?? (await _salesService.CreateCustomerAsync(new CreateCustomerDto
            {
                Name = order.CustomerName,
                Phone = order.CustomerPhone,
                Address = $"{order.Province}، {order.City}، {order.AddressLine}"
            })).Id;

        var invoiceId = await _salesService.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customerId,
            WarehouseId = _store.WarehouseId,
            DiscountAmount = order.DiscountAmount,
            // حمل‌ونقل باید در فاکتور هم باشد تا TotalAmount دقیقاً برابر مبلغ پرداختی مشتری (Order.Total) شود
            ShippingAmount = order.ShippingCost,
            Notes = $"سفارش آنلاین {order.OrderNumber}" + (refId is null ? "" : $" — شماره پیگیری: {refId}") + noteSuffix,
            Items = order.Items.Select(i => new SalesInvoiceItemInput
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        }, "store");

        try
        {
            await _salesService.ConfirmInvoiceAsync(invoiceId, "store");
        }
        catch
        {
            // تأیید ناموفق (مثلاً اتمام موجودی بین ثبت سفارش و پرداخت) نباید پیش‌نویس را یتیم بگذارد؛
            // لغوشدنِ پیش‌نویس اثر انباری/حسابداری ندارد و شمارهٔ سفارش برای پیگیری می‌ماند
            try { await _salesService.CancelInvoiceAsync(invoiceId, "store"); }
            catch { /* بهترین‌تلاش */ }
            throw;
        }

        return invoiceId;
    }

    /// <summary>
    /// ثبت خودکار رسید دریافت پس از پرداخت آنلاین (بدهکار صندوق/بانک انتخابی، بستانکار حساب‌های دریافتنی).
    /// فقط وقتی «Store:OnlinePaymentFinancialAccountId» تنظیم شده انجام می‌شود و هیچ‌وقت خطا را به پرداخت تزریق نمی‌کند.
    /// </summary>
    private async Task TryRegisterOnlinePaymentReceiptAsync(Order order, int? invoiceId, string? refId)
    {
        if (_store.OnlinePaymentFinancialAccountId is not int accountId)
            return;

        if (invoiceId is null)
        {
            _logger.LogWarning("Automatic payment receipt skipped for order {OrderNumber}: invoice was not created", order.OrderNumber);
            return;
        }

        try
        {
            var invoice = await _salesService.GetInvoiceAsync(invoiceId.Value);
            var amount = invoice?.TotalAmount ?? order.Total;
            if (invoice is not null && invoice.TotalAmount != order.Total)
                _logger.LogWarning("Invoice total {InvoiceTotal} differs from order total {OrderTotal} for {OrderNumber}",
                    invoice.TotalAmount, order.Total, order.OrderNumber);

            await _treasury.RegisterCustomerReceiptAsync(new CreateCustomerReceiptDto
            {
                CustomerId = invoice?.CustomerId,
                FinancialAccountId = accountId,
                Amount = amount,
                Method = nameof(PaymentMethod.BankTransfer),
                ReceiptDate = DateTime.UtcNow,
                Notes = $"پرداخت آنلاین — سفارش {order.OrderNumber}" + (refId is null ? "" : $" — پیگیری: {refId}")
            }, "system");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register automatic receipt for order {OrderNumber}", order.OrderNumber);
        }
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
