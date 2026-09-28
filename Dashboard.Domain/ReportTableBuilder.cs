using System.Globalization;
using System.Reflection;
using Dashboard.Domain.Queries;

namespace Dashboard.Domain.Queries;

/// <summary>
/// ساخت <see cref="ReportTable"/> از یک لیست DTO با استفاده از Reflection.
/// کمک می‌کند هر گزارش فقط ستون‌ها و سطرها را بنویسد، نه منطق نگاشت را.
/// </summary>
public static class ReportTableBuilder
{
    /// <summary>
    /// اعمال صفحه‌بندی/سقف روی ردیف‌های نهایی و ساخت جدول.
    /// <para>
    /// این نقطهٔ واحدِ اعمالِ <see cref="ReportQueryOptions"/> است تا هر گزارش
    /// رفتار یکسانی داشته باشد: مسیر نمایش با Skip/Take بریده می‌شود و پرچم
    /// <c>Truncated</c> روشن می‌ماند؛ مسیر خروجی (Take = null) هیچ برشی نمی‌خورد،
    /// پس اکسل همیشه «همهٔ» رکوردهای فیلترشده را دارد.
    /// </para>
    /// <para>
    /// <paramref name="totalCount"/> باید تعداد کل ردیف‌ها <b>پیش از</b> برش باشد،
    /// وگرنه صفحه‌بندی و پیام «۱۰ از ۳۰» اشتباه می‌شود.
    /// </para>
    /// </summary>
    public static ReportTable BuildPaged<T>(
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<T> allRows,
        ReportQueryOptions? options)
    {
        var opts = options ?? ReportQueryOptions.ForPage(1);
        var total = allRows.Count;

        if (opts.Take is null)
            return Build(columns, allRows, total);   // خروجی: بدون برش

        var page = opts.Skip >= total
            ? new List<T>()
            : allRows.Skip(opts.Skip).Take(opts.Take.Value).ToList();

        // «بریده شده» یعنی رکورد بیشتری خارج از این صفحه/سقف وجود دارد
        var truncated = total > opts.Skip + page.Count;

        return Build(columns, page, total, truncated);
    }

    /// <summary>
    /// نسخهٔ مخصوص ردیف‌هایی که از قبل دیکشنری‌اند (مثلاً وقتی یک گزارش از
    /// گزارش دیگری مشتق می‌شود). <see cref="Build{T}"/> روی دیکشنری کار نمی‌کند
    /// چون reflection روی propertyها انجام می‌شود و دیکشنری property ندارد —
    /// نتیجه این می‌شد که همهٔ سلول‌ها null شوند. اینجا مستقیم از خود کلیدها خوانده
    /// می‌شود.
    /// </summary>
    public static ReportTable BuildPaged(
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> allRows,
        ReportQueryOptions? options)
    {
        var opts = options ?? ReportQueryOptions.ForPage(1);
        var total = allRows.Count;

        if (opts.Take is null)
            return BuildDictionary(columns, allRows, total);

        var page = opts.Skip >= total
            ? new List<IReadOnlyDictionary<string, object?>>()
            : allRows.Skip(opts.Skip).Take(opts.Take.Value).ToList();

        var truncated = total > opts.Skip + page.Count;
        return BuildDictionary(columns, page, total, truncated);
    }

    private static ReportTable BuildDictionary(
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        int totalCount,
        bool truncated = false)
    {
        // هر سطر به دیکشنریِ قابل‌نوشتن تبدیل می‌شود تا ستون‌های اضافه (مثل Loss)
        // واقعاً در خروجی باشند، ولی سطرهای ورودی دست‌نخورده بمانند.
        var normalized = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)r
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new ReportTable(columns, normalized, truncated, totalCount);
    }

    /// <summary>
    /// نگاشت لیست اشیاء به سطرهای جدول بر اساس <see cref="ReportColumn.Key"/>.
    /// فقط ستون‌هایی که واقعاً روی نوع وجود دارند نگاشته می‌شوند؛ ستونِ ناسازگار
    /// به‌جای کرش، مقدار null می‌گیرد (تا افزودن/تغییر یک فیلد، گزارش را نشکند).
    /// </summary>
    public static ReportTable Build<T>(
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<T> items,
        int totalCount,
        bool truncated = false)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var item in items)
        {
            var row = new Dictionary<string, object?>(columns.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns)
                row[col.Key] = props.TryGetValue(col.Key, out var p) ? p.GetValue(item) : null;
            rows.Add(row);
        }

        return new ReportTable(columns, rows, truncated, totalCount);
    }
}
