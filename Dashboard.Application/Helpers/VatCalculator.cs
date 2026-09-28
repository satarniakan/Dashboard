namespace Dashboard.Application.Helpers;

/// <summary>
/// محاسبهٔ مالیات بر ارزش افزوده — تنها نقطهٔ محاسبه در کل سیستم تا گرد کردن
/// بین سفارش فروشگاه و فاکتور ساخته‌شده از همان سفارش هیچ‌وقت اختلاف نکند
/// (اختلاف چند ریالی، تراکنشِ درگاه را در مرحلهٔ verify رد می‌کند).
/// مالکیتِ مالیات: فروش → ۲۳۰۰ بستانکار؛ خرید → ۱۳۵۰ بدهکار (اعتبار مالیاتی).
/// </summary>
public static class VatCalculator
{
    public static decimal Calculate(decimal taxableBase, decimal percent) =>
        percent <= 0
            ? 0m
            : Math.Round(taxableBase * percent / 100m, 2, MidpointRounding.AwayFromZero);
}
