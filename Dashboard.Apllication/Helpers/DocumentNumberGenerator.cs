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
        var datePart = $"{pc.GetYear(date):0000}{pc.GetMonth(date):00}{pc.GetDayOfMonth(date):00}";
        var relatedPart = (relatedId ?? 0).ToString();
        return $"{datePart}-{relatedPart}-{recordId}";
    }
}