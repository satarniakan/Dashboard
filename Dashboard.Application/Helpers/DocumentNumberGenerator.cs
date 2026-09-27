using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class DocumentNumberGenerator
{
    /// <summary>
    /// می‌سازد: تاریخ‌شمسی-شناسه‌مربوطه-شماره‌رکورد (مثلاً 14050618-12-7)
    /// relatedId اگر وجود نداشته باشد، صفر گذاشته می‌شود.
    /// </summary>
    public static string Generate(DateTime date, int? relatedId, int recordId)
    {
        var pc = new PersianCalendar();
        // تاریخ‌ها در دیتابیس UTC ذخیره می‌شوند؛ بخشِ تاریخِ شماره باید مثل ToPersianDate
        // روی ساعت تهران محاسبه شود، وگرنه در ۰۰:۰۰–۰۳:۳۰ تهران یک روز از تاریخ نمایشی عقب می‌ماند.
        var tehran = PersianDateHelper.ToTehran(date);
        var datePart = $"{pc.GetYear(tehran):0000}{pc.GetMonth(tehran):00}{pc.GetDayOfMonth(tehran):00}";
        var relatedPart = (relatedId ?? 0).ToString();
        return $"{datePart}-{relatedPart}-{recordId}";
    }
}