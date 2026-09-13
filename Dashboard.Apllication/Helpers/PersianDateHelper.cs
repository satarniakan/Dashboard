using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Calendar = new();

    public static string ToPersianDate(this DateTime dateTime)
    {
        var raw = $"{Calendar.GetYear(dateTime):0000}/{Calendar.GetMonth(dateTime):00}/{Calendar.GetDayOfMonth(dateTime):00}";
        return ToPersianDigits(raw);
    }

    public static string ToPersianDateTime(this DateTime dateTime)
    {
        var raw = $"{Calendar.GetYear(dateTime):0000}/{Calendar.GetMonth(dateTime):00}/{Calendar.GetDayOfMonth(dateTime):00} {dateTime:HH:mm}";
        return ToPersianDigits(raw);
    }

    private static string ToPersianDigits(string input)
    {
        string[] persian = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };
        return string.Concat(input.Select(c => char.IsDigit(c) ? persian[c - '0'] : c.ToString()));
    }
}