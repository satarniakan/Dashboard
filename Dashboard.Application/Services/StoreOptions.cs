// Dashboard.Application/Services/StoreOptions.cs
namespace Dashboard.Application.Services;

/// <summary>
/// تنظیمات فروشگاه — از بخش «Store» در appsettings خوانده می‌شود (Bind شده با Configure&lt;StoreOptions&gt;).
/// </summary>
public class StoreOptions
{
    public const string SectionName = "Store";

    /// <summary>انبار مبدأ سفارش‌های آنلاین</summary>
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
}
