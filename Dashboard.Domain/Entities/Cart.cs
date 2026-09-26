// Dashboard.Domain/Entities/Cart.cs
namespace Dashboard.Domain.Entities;

/// <summary>سبد خرید فروشگاه — برای مهمان با شناسه‌ی کوکی شناسایی می‌شود و بعد از لاگین به کاربر وصل می‌شود.</summary>
public class Cart
{
    public int Id { get; set; }
    public string CookieId { get; set; } = string.Empty;

    /// <summary>کد تخفیف اعمال‌شده روی سبد (اعتبارسنجی مجدد هنگام تسویه)</summary>
    public int? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>سبدهای منقضی‌شده قابل استفاده نیستند و به‌صورت دوره‌ای پاک می‌شوند.</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(1);

    public List<CartItem> Items { get; set; } = new();
}
