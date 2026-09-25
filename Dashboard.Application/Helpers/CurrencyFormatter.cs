// Dashboard.Application/Helpers/CurrencyFormatter.cs
using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class CurrencyFormatter
{
    // نکته‌ی مهم: به‌جای اعتماد به CultureInfo("fa-IR") (که بسته به نسخه‌ی دات‌نت/سیستم‌عامل
    // ممکن است جداکننده‌های عربی «٬» و «٫» را به‌جای «,» و «.» برگرداند)، خودمان صریح یک
    // قالب اعداد می‌سازیم تا همیشه، روی هر سرور و هر نسخه‌ای، خروجی یکسان و قابل‌پیش‌بینی باشد.
    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = "."
    };

    public static string ToToman(decimal amount)
    {
        var formattedNumber = amount.ToString("#,0", Format);
        return $"{formattedNumber} تومان";
    }
}
