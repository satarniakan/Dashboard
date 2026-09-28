namespace Dashboard.Domain.Queries;

/// <summary>
/// فیلتر مشترک بین «نمایش در صفحه» و «خروجی اکسل».
/// هر دو از یک شیء یکسان می‌خوانند تا کاربر دقیقاً همان چیزی را دانلود کند
/// که روی صفحه می‌بیند — اگر این دو مسیر جدا از هم فیلتر کنند، عدد گزارش با
/// آنچه کاربر می‌بیند فرق می‌کند.
/// </summary>
public class ReportFilter
{
    /// <summary>از این تاریخ (شامل) — null یعنی بدون محدودیت</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>تا این تاریخ (شامل، تا پایان روز)</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>جست‌وجوی متنی آزاد در شماره/نام/نام کالا</summary>
    public string? Search { get; set; }

    public bool HasDateRange => FromDate.HasValue || ToDate.HasValue;

    /// <summary>پاک‌سازی ورودی‌های خالی — ویژگی‌های null روی کوئری معنی ندارند</summary>
    public ReportFilter Normalized() => new()
    {
        FromDate = FromDate?.Date,
        ToDate = ToDate?.Date,
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()
    };
}
