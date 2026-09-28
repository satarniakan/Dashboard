namespace Dashboard.Domain.Queries;

/// <summary>نوع دادهٔ ستون — تعیین می‌کند اکسل چگونه مقدار را بنویسد و فیلتر چه کادری بگیرد</summary>
public enum ReportColumnKind
{
    Text,
    Number,
    Money,
    Date,
    Integer
}

/// <summary>
/// توصیف یک ستون گزارش. هر صفحه فیلدهای خودش را تعریف می‌کند (انعطاف‌پذیری)،
/// ولی ساختار یکسان است تا کامپوننت جدول بتواند همه را یکسان رندر کند.
/// </summary>
public sealed record ReportColumn(
    string Key,          // نام خاصیت روی DTO
    string Title,        // عنوان فارسی ستون
    ReportColumnKind Kind = ReportColumnKind.Text)
{
    /// <summary>عرض ترجیحی در اکسل (اختیاری)</summary>
    public double? Width { get; init; }
}

/// <summary>
/// گزینه‌های اجرای گزارش — تعیین می‌کند چقدر از نتیجه برگردد.
/// <para>
/// چرا این تفکیک لازم است: «نمایش روی صفحه» و «خروجی اکسل» دو نیاز متفاوت
/// دارند. نمایش باید سبک و صفحه‌بندی‌شده باشد (وگرنه مرورگر با صد هزار ردیف
/// هنگ می‌کند)، ولی خروجی اکسل طبق خواستهٔ کاربر باید «همهٔ» رکوردهای فیلترشده
/// را داشته باشد. اگر هر دو از یک مسیر بخوانند، یا خروجی ناقص می‌شود یا صفحه
/// کند می‌شود. پس <see cref="Take"/> برای نمایش و <c>null</c> برای خروجی است.
/// </para>
/// </summary>
public sealed record ReportQueryOptions
{
    /// <summary>حداکثر تعداد ردیفِ برگشتی. <c>null</c> یعنی بدون سقف (مسیر خروجی).</summary>
    public int? Take { get; init; }

    /// <summary>تعداد ردیف‌هایی که باید رد شوند — برای صفحه‌بندی نمایش.</summary>
    public int Skip { get; init; }

    /// <summary>تعداد ردیف در هر صفحه (فقط برای نمایش).</summary>
    public int PageSize { get; init; } = 500;

    /// <summary>گزینه‌های پیش‌فرض: نمایشِ صفحه‌بندی‌شده با ۵۰۰ ردیف در هر صفحه.</summary>
    public static ReportQueryOptions ForPage(int page, int pageSize = 500) => new()
    {
        Skip = Math.Max(0, page - 1) * Math.Max(1, pageSize),
        Take = Math.Max(1, pageSize)
    };

    /// <summary>گزینه‌های خروجی: بدون سقف و بدون رد کردن — همهٔ رکوردهای فیلترشده.</summary>
    public static ReportQueryOptions ForExport() => new() { Take = null, Skip = 0 };
}

/// <summary>نتیجهٔ یک گزارش: سرستون‌ها + سطرها</summary>
public sealed record ReportTable(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated = false,
    int? TotalCount = null);

/// <summary>
/// الگوی مشترک همهٔ گزارش‌ها.
/// <see cref="TotalCount"/> تعداد کل رکوردها بدون اعمال صفحه‌بندی است — برای
/// دانلود اکسل استفاده می‌شود که باید «همهٔ» رکوردهای فیلترشده باشد، نه فقط
/// صفحهٔ نمایش‌داده‌شده.
/// </summary>
public interface IReportQuery
{
    Task<ReportTable> GetAsync(ReportFilter filter, CancellationToken ct = default);
}
