// Dashboard.Application/Services/StoreOptions.cs
namespace Dashboard.Application.Services;

/// <summary>
/// تنظیمات فروشگاه — از بخش «Store» در appsettings خوانده می‌شود (Bind شده با Configure&lt;StoreOptions&gt;).
/// </summary>
public class StoreOptions
{
    public const string SectionName = "Store";

    /// <summary>
    /// انبار مبدأ سفارش‌های آنلاین. این انبار خودکار ساخته نمی‌شود: باید در بخش
    /// «انبارها» ساخته شده باشد و شماره‌اش همین‌جا ست شود، وگرنه ثبت سفارش رد می‌شود.
    /// </summary>
    public int WarehouseId { get; set; } = 1;

    public string Name { get; set; } = "فروشگاه من";

    /// <summary>هزینه‌ی ارسال پست پیشتاز (تومان)</summary>
    public decimal PostShippingCost { get; set; } = 80_000m;

    /// <summary>هزینه‌ی ارسال پیک (تومان)</summary>
    public decimal CourierShippingCost { get; set; } = 50_000m;

    /// <summary>هزینه‌ی تحویل حضوری (تومان)</summary>
    public decimal InPersonShippingCost { get; set; } = 0m;

    public decimal GetShippingCost(Domain.Enums.ShippingMethod method) => method switch
    {
        Domain.Enums.ShippingMethod.Post => PostShippingCost,
        Domain.Enums.ShippingMethod.Courier => CourierShippingCost,
        Domain.Enums.ShippingMethod.InPerson => InPersonShippingCost,
        _ => 0m
    };

    /// <summary>
    /// شناسهٔ صندوق/بانکِ پرداخت آنلاین — اگر تنظیم شود، پس از پرداخت موفق سفارش،
    /// رسید دریافت به‌صورت خودکار ثبت می‌شود (بدهکار این صندوق/بانک، بستانکار حساب‌های دریافتنی).
    /// خالی یعنی مثل قبل رسیدها دستی ثبت می‌شوند.
    /// </summary>
    public int? OnlinePaymentFinancialAccountId { get; set; }

    /// <summary>
    /// مهلت پرداخت سفارش (ساعت) — بعد از این مدت، سفارشِ پرداخت‌نشده خودکار لغو و
    /// موجودی رزروشدهٔ آن آزاد می‌شود. ۶ ساعت پیش‌فرض است چون بانک/درگاه ممکن است
    /// کاربر را چند ساعت روی صفحهٔ پرداخت نگه دارد.
    /// </summary>
    public int OrderPaymentWindowHours { get; set; } = 6;

    /// <summary>
    /// درصد مالیات بر ارزش افزودهٔ فروشگاه — مبنای محاسبه: جمع اقلام منهای تخفیف به‌اضافهٔ
    /// هزینهٔ ارسال. صفر (پیش‌فرض) یعنی فروشگاه مالیاتی نمی‌گیرد و رفتار قبلی حفظ می‌شود.
    /// نرخ بر خودِ سفارش عکس‌برداری می‌شود؛ تغییر نرخ، سفارش‌های قبلی را تغییر نمی‌دهد.
    /// </summary>
    public decimal VatRate { get; set; } = 0m;
}
