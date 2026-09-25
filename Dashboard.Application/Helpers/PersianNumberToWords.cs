// Dashboard.Application/Helpers/PersianNumberToWords.cs
namespace Dashboard.Application.Helpers;

public static class PersianNumberToWords
{
    private static readonly string[] Ones = { "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه" };
    private static readonly string[] Teens = { "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
    private static readonly string[] Tens = { "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود" };
    private static readonly string[] Hundreds = { "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد" };
    private static readonly string[] Scales = { "", " هزار", " میلیون", " میلیارد" };

    public static string ToWords(long n)
    {
        if (n == 0) return "صفر";
        var parts = new List<string>();
        for (var scale = Scales.Length - 1; scale >= 0; scale--)
        {
            long group = n / (long)Math.Pow(1000, scale) % 1000;
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
        return string.Join(" و ", parts);
    }

    // قرارداد فارسی: «هزار» بدون «یک»، از میلیون به بالا «یک میلیون»
    public static string ToTomanWords(long amount)
        => amount == 0 ? "صفر تومان" : ToWords(amount) + " تومان";
}
