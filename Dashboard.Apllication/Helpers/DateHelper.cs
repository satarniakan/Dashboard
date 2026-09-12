// Dashboard.Application/Helpers/DateHelper.cs
using System.Globalization;

namespace Dashboard.Application.Helpers;

public static class DateHelper
{
    private static readonly PersianCalendar _pc = new();

    /// <summary>تبدیل DateTime میلادی به رشته شمسی (۱۴۰۳/۰۱/۱۵)</summary>
    public static string ToShamsi(this DateTime date)
    {
        int year = _pc.GetYear(date);
        int month = _pc.GetMonth(date);
        int day = _pc.GetDayOfMonth(date);
        return $"{year:D4}/{month:D2}/{day:D2}";
    }

    /// <summary>تبدیل DateTime? میلادی به رشته شمسی</summary>
    public static string ToShamsi(this DateTime? date)
        => date.HasValue ? date.Value.ToShamsi() : string.Empty;

    /// <summary>تبدیل DateOnly میلادی به رشته شمسی</summary>
    public static string ToShamsi(this DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue).ToShamsi();

    /// <summary>تبدیل رشته شمسی (۱۴۰۳/۰۱/۱۵) به DateTime میلادی</summary>
    public static DateTime? FromShamsi(string? shamsiDate)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate)) return null;

        var parts = shamsiDate.Trim().Split('/');
        if (parts.Length != 3) return null;

        if (int.TryParse(parts[0], out int year) &&
            int.TryParse(parts[1], out int month) &&
            int.TryParse(parts[2], out int day))
        {
            try
            {
                return _pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }
}
