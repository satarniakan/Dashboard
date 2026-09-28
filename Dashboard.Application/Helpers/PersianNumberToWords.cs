// Dashboard.Application/Helpers/PersianNumberToWords.cs
using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class PersianNumberToWords
{
    private static readonly string[] Ones = { "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه" };
    private static readonly string[] Teens = { "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
    private static readonly string[] Tens = { "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود" };
    private static readonly string[] Hundreds = { "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد" };
    private static readonly string[] Scales = { "", " هزار", " میلیون", " میلیارد", " هزار میلیارد" };

    // مقیاس‌ها از پیش حساب شده‌اند: (long)Math.Pow(1000, 4) روی عدد بزرگ خطای گردکردن می‌داد
    private static readonly decimal[] Divisors =
        { 1m, 1_000m, 1_000_000m, 1_000_000_000m, 1_000_000_000_000m };

    // «هزار میلیارد» آخرین مقیاس است ⇒ تا ۹۹۹,۹۹۹,۹۹۹,۹۹۹,۹۹۹ حرف‌ها دارد
    private const decimal MaxSupported = 1_000_000_000_000_000m;

    public static string ToWords(long n)
    {
        if (n == 0) return "صفر";

        // مبلغ منفی (ماندهٔ بدهکار، اختلاف انبار) قبلاً رشتهٔ خالی می‌داد چون همهٔ گروه‌ها
        // «صفر یا منفی» رد می‌شدند. قدرمطلق روی decimal گرفته می‌شود: ‎-long.MinValue سرریز است.
        var negative = n < 0;
        var magnitude = negative ? -(decimal)n : (decimal)n;

        // عدد بیرون از جدول مقیاس: پرتاب نمی‌کنیم — این تابع هنگام تایپ در AppPriceField
        // صدا زده می‌شود و exception یعنی صفحهٔ کرش‌کرده. رقم‌های گروهی‌شده می‌دهیم.
        if (magnitude >= MaxSupported)
            return (negative ? "منهای " : "") + magnitude.ToString("N0", CultureInfo.InvariantCulture);

        var parts = new List<string>();
        for (var scale = Scales.Length - 1; scale >= 0; scale--)
        {
            long group = (long)(magnitude / Divisors[scale] % 1000);
            if (group == 0) continue;
            var words = new List<string>();
            if (group >= 100) { words.Add(Hundreds[group / 100]); group %= 100; }
            if (group >= 10 && group < 20) words.Add(Teens[group - 10]);
            else
            {
                if (group >= 20) { words.Add(Tens[group / 10]); group %= 10; }
                if (group > 0) words.Add(Ones[group]);
            }
            parts.Add(string.Join(" و ", words) + Scales[scale]);
        }
        // «یک» ابتدای هزار گفته نمی‌شود: 1000 = «هزار»، ولی 1,001,000 = «یک میلیون و یک هزار»
        if (parts.Count > 0 && parts[0] == "یک هزار") parts[0] = "هزار";
        return (negative ? "منهای " : "") + string.Join(" و ", parts);
    }

    // قرارداد فارسی: «هزار» بدون «یک»، از میلیون به بالا «یک میلیون»
    public static string ToTomanWords(long amount)
        => amount == 0 ? "صفر تومان" : ToWords(amount) + " تومان";
}
