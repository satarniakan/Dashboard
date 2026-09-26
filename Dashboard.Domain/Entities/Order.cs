// Dashboard.Domain/Entities/Order.cs
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>
/// سفارش فروشگاه اینترنتی — پس از پرداخت موفق به فاکتور فروش (SalesInvoice) تبدیل می‌شود
/// تا انبار و حسابداری از همان مسیر فعلی سیستم تغذیه شوند.
/// اطلاعات مشتری/آدرس به‌صورت snapshot ذخیره می‌شود تا تغییرات بعدی پروفایل، سفارش‌های past را خراب نکند.
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>شماره‌ی سفارش یکتا برای نمایش به مشتری (مثل ORD-14040612-XXXX)</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>شناسه‌ی کاربر Identity (رشته‌ی GUID) که سفارش را ثبت کرده</summary>
    public string UserId { get; set; } = string.Empty;

    // --- Snapshot مشتری و آدرس ارسال ---
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;

    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.Post;
    public decimal ShippingCost { get; set; }

    // --- مبالغ (تومان) ---
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountCodeText { get; set; }
    public decimal Total => Subtotal - DiscountAmount + ShippingCost;

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    /// <summary>فاکتور فروش متناظر — پس از پرداخت موفق ساخته می‌شود</summary>
    public int? SalesInvoiceId { get; set; }

    /// <summary>کد رهگیری پست — توسط ادمین پس از ارسال ثبت می‌شود</summary>
    public string? TrackingCode { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}
