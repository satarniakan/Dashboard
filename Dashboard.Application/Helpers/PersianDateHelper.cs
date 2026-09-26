using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Calendar = new();

    // همه‌ی زمان‌ها در دیتابیس UTC ذخیره می‌شوند؛ تقویم شمسی باید روی ساعت تهران
    // اعمال شود، وگرنه رویدادهای ۲۰:۳۰ تا نیمه‌شب با تاریخ روز قبل نمایش داده می‌شوند.
    // ایران از ۱۴۰۱ ساعت تابستانی ندارد، پس اختلاف همیشه ۳:۳۰+ ثابت است.
    private static readonly TimeSpan TehranOffset = new(3, 30, 0);

    private static DateTime ToTehran(DateTime dateTime) =>
        dateTime.Kind == DateTimeKind.Local ? dateTime : dateTime.Add(TehranOffset);

    public static string ToPersianDate(this DateTime dateTime)
    {
        var tehran = ToTehran(dateTime);
        var raw = $"{Calendar.GetYear(tehran):0000}/{Calendar.GetMonth(tehran):00}/{Calendar.GetDayOfMonth(tehran):00}";
        return ToPersianDigits(raw);
    }

    public static string ToPersianDateTime(this DateTime dateTime)
    {
        var tehran = ToTehran(dateTime);
        var raw = $"{Calendar.GetYear(tehran):0000}/{Calendar.GetMonth(tehran):00}/{Calendar.GetDayOfMonth(tehran):00} {tehran:HH:mm}";
        return ToPersianDigits(raw);
    }

    private static string ToPersianDigits(string input)
    {
        string[] persian = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };
        return string.Concat(input.Select(c => char.IsDigit(c) ? persian[c - '0'] : c.ToString()));
    }
}