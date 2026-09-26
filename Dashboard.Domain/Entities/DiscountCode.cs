// Dashboard.Domain/Entities/DiscountCode.cs
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>کد تخفیف فروشگاه — قواعد مصرف در سرویس سبد/پرداخت اعتبارسنجی می‌شود.</summary>
public class DiscountCode
{
    public int Id { get; set; }

    /// <summary>کد یکتا — همیشه با حروف بزرگ نگه‌داری می‌شود</summary>
    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>برای نوع Percentage بین ۰ تا ۱۰۰؛ برای FixedAmount مبلغ به تومان</summary>
    public decimal Value { get; set; }

    /// <summary>سقف مبلغ تخفیف برای کدهای درصدی (اختیاری)</summary>
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>حداقل جمع سبد برای امکان استفاده (اختیاری)</summary>
    public decimal? MinCartAmount { get; set; }

    /// <summary>سقف تعداد استفاده‌ی کل؛ null یعنی نامحدود</summary>
    public int? MaxUsageCount { get; set; }

    /// <summary>سقف تعداد استفاده برای هر مشتری؛ null یعنی نامحدود (اجرای محدودسازی در فاز ۴ با ثبت سفارش)</summary>
    public int? MaxUsagePerCustomer { get; set; }

    /// <summary>شمارنده‌ی مصرف — هنگام ثبت سفارش (فاز ۴) افزایش می‌یابد</summary>
    public int UsageCount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>اعتبارسنجی وضعیت/زمان/سقف مصرف (بدون سبد) — برای نمایش مجدد در سبد</summary>
    public bool IsValidNow(DateTime utcNow, int? currentUsage = null)
    {
        if (!IsActive) return false;
        if (StartsAt is not null && utcNow < StartsAt.Value) return false;
        if (ExpiresAt is not null && utcNow > ExpiresAt.Value) return false;
        if (MaxUsageCount is not null && (currentUsage ?? UsageCount) >= MaxUsageCount.Value) return false;
        return true;
    }

    /// <summary>محاسبه‌ی مبلغ تخفیف برای یک جمع سبد</summary>
    public decimal CalculateDiscount(decimal cartTotal)
    {
        if (MinCartAmount is not null && cartTotal < MinCartAmount.Value)
            return 0m;

        var amount = Type switch
        {
            DiscountType.Percentage => cartTotal * (Value / 100m),
            DiscountType.FixedAmount => Value,
            _ => 0m
        };

        if (Type == DiscountType.Percentage && MaxDiscountAmount is not null)
            amount = Math.Min(amount, MaxDiscountAmount.Value);

        // تخفیف نمی‌تواند از جمع سبد بیشتر شود
        return Math.Clamp(amount, 0m, cartTotal);
    }
}
