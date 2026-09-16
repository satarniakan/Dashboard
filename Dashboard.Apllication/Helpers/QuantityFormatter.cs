// Dashboard.Application/Helpers/QuantityFormatter.cs
using System.Globalization;

namespace Dashboard.Application.Helpers;

/// <summary>
/// چرا این کلاس لازم بود: ستون‌های مقدار (Quantity, QuantityOnHand, QuantityChange و ...) توی دیتابیس
/// از نوع decimal(18,3) هستند (برای پشتیبانی از کالاهای وزنی/کسری مثل ۲.۵ کیلو). وقتی SQL Server
/// یک عدد صحیح مثل ۲ رو از همچین ستونی برمی‌گردونه، همراه با مقیاس کامل ستون (یعنی 2.000) برمی‌گرده.
/// اگه این مقدار مستقیم و بدون فرمت توی صفحه چاپ بشه (`@item.Quantity`)، همون "2.000" نمایش داده می‌شه،
/// نه یک عدد ساده‌ی "۲". این متد صفرهای اضافه‌ی بعد از اعشار رو حذف می‌کنه، ولی اگه مقدار واقعاً
/// کسری بود (مثل ۲.۵) درست نگهش می‌داره.
/// </summary>
public static class QuantityFormatter
{
    // همون دلیل CurrencyFormatter: به‌جای CultureInfo("fa-IR")، جداکننده‌ها رو صریح مشخص می‌کنیم
    // تا همیشه «,» و «.» باشن، نه جداکننده‌های عربی «٬»/«٫» که بسته به نسخه‌ی دات‌نت ممکنه برگرده.
    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = "."
    };

    public static string Seperator(decimal quantity)
    {
        // "#,0.###" یعنی: جداکننده‌ی هزارگان بذار، تا ۳ رقم اعشار نشون بده ولی صفرهای
        // اضافه‌ی انتهایی رو (اگه لازم نبودن) حذف کن.
        return quantity.ToString("#,0.###", Format);
    }
}
