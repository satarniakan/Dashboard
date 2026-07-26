// Dashboard.Application/Helpers/CurrencyFormatter.cs
using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class CurrencyFormatter
{
    private static readonly CultureInfo PersianCulture = new("fa-IR");

    public static string ToToman(decimal amount)
    {
        // Format the number with Persian digits and thousand separators
        var formattedNumber = amount.ToString("#,0", PersianCulture);
        return $"{formattedNumber} تومان";
    }
}