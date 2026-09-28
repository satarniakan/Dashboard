using Dashboard.Domain.Queries;
using Xunit;

namespace Dashboard.Tests;

/// <summary>
/// منطق برش/صفحه‌بندی گزارش‌ها. این هستهٔ «جداسازی مسیر نمایش از خروجی» است:
/// نمایش باید کوتاه شود، ولی خروجی اکسل نباید هیچ رکوردی را از دست بدهد.
/// چون منطق خالص است، اینجا بدون دیتابیس و سریع تست می‌شود.
/// </summary>
public class ReportTableBuilderTests
{
    private sealed record Row(int Id, decimal Amount);

    private static readonly IReadOnlyList<ReportColumn> Cols =
    [
        new("Id", "شناسه", ReportColumnKind.Integer),
        new("Amount", "مبلغ", ReportColumnKind.Money)
    ];

    /// <summary>۱۰ ردیف با شناسه‌های ۱ تا ۱۰</summary>
    private static List<Row> Rows(int count = 10) =>
        Enumerable.Range(1, count).Select(i => new Row(i, i * 100m)).ToList();

    [Fact]
    public void ExportOptions_ReturnEveryRow_Uncapped()
    {
        var table = ReportTableBuilder.BuildPaged(Cols, Rows(), ReportQueryOptions.ForExport());

        Assert.Equal(10, table.Rows.Count);
        Assert.Equal(10, table.TotalCount);
        Assert.False(table.Truncated);
    }

    [Fact]
    public void DisplayOptions_TrimToPageSize_ButReportTrueTotal()
    {
        var table = ReportTableBuilder.BuildPaged(Cols, Rows(), ReportQueryOptions.ForPage(1, 3));

        Assert.Equal(3, table.Rows.Count);
        // نکتهٔ کلیدی: مجموع باید «قبل از برش» باشد تا شمارش صفحه درست کار کند
        Assert.Equal(10, table.TotalCount);
        Assert.True(table.Truncated);
    }

    [Fact]
    public void Pages_AreDisjointAndCoverEverything()
    {
        var all = Rows();
        var seen = new List<int>();

        for (var page = 1; page <= 4; page++)
        {
            var result = ReportTableBuilder.BuildPaged(Cols, all, ReportQueryOptions.ForPage(page, 3));
            seen.AddRange(result.Rows.Select(r => Convert.ToInt32(r["Id"])));
        }

        Assert.Equal(all.Count, seen.Count);                  // هیچ ردیفی گم نشد
        Assert.Equal(seen.Count, seen.Distinct().Count());    // هیچ ردیفی تکرار نشد
        Assert.Equal(seen.OrderBy(i => i), all.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void LastPage_IsPartial_AndNotTruncated()
    {
        // ۱۰ ردیف با صفحه‌بندی ۳ تایی ⇒ صفحهٔ ۴ فقط یک ردیف دارد و صفحهٔ پایانی است
        var page4 = ReportTableBuilder.BuildPaged(Cols, Rows(), ReportQueryOptions.ForPage(4, 3));
        Assert.Single(page4.Rows);
        Assert.False(page4.Truncated);   // بعد از این صفحه چیزی نمی‌ماند

        // صفحهٔ ۳ هنوز یک ردیف بعد از خودش دارد ⇒ Truncated درست است
        var page3 = ReportTableBuilder.BuildPaged(Cols, Rows(), ReportQueryOptions.ForPage(3, 3));
        Assert.Equal(3, page3.Rows.Count);
        Assert.True(page3.Truncated);
    }

    [Fact]
    public void PageBeyondEnd_ReturnsEmpty_NotCrash()
    {
        var table = ReportTableBuilder.BuildPaged(Cols, Rows(), ReportQueryOptions.ForPage(99, 3));

        Assert.Empty(table.Rows);
        Assert.Equal(10, table.TotalCount);
    }

    [Fact]
    public void NullOptions_DefaultToFirstPage_SoPageIsNeverUnbounded()
    {
        // حالت پیش‌فرض باید نمایشِ کوتاه باشد، نه خروجیِ بی‌سقف. اگر روزی
        // کسی این پیش‌فرض را عوض کند، جدول UI می‌تواند بی‌نهایت بزرگ شود.
        // ۱۲۰۰ ردیف می‌سازیم تا واقعاً از سقف ۵۰۰ عبور کند.
        var table = ReportTableBuilder.BuildPaged<Row>(Cols, Rows(1200), null);

        Assert.Equal(500, ReportQueryOptions.ForPage(1).Take);
        Assert.Equal(500, table.Rows.Count);   // نه ۱۲۰۰ ⇒ پیش‌فرض واقعاً برش می‌دهد
        Assert.Equal(1200, table.TotalCount);
        Assert.True(table.Truncated);
    }

    [Fact]
    public void DictionaryRows_KeepTheirKeys_InsteadOfBecomingNull()
    {
        // گزارش کالاهای زیان‌ده از گزارش کالا مشتق می‌شود و ردیف‌هایش دیکشنری‌اند.
        // اگر نگاشتِ reflection روی دیکشنری اجرا شود، همهٔ سلول‌ها null می‌شوند.
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Id"] = 1, ["Amount"] = 50m },
            new Dictionary<string, object?> { ["Id"] = 2, ["Amount"] = -20m }
        };

        var table = ReportTableBuilder.BuildPaged(Cols, rows, ReportQueryOptions.ForExport());

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(1, Convert.ToInt32(table.Rows[0]["Id"]));
        Assert.Equal(50m, Convert.ToDecimal(table.Rows[0]["Amount"]));
    }

    [Fact]
    public void MissingColumn_YieldsNull_AndDoesNotThrow()
    {
        // گزارشی که ستونی کم دارد نباید کل جدول را خراب کند
        var row = new Row(1, 100m);
        var table = ReportTableBuilder.Build(Cols, new[] { row }, 1);
        Assert.NotNull(table.Rows[0]);

        var withExtra = ReportTableBuilder.Build(
            [.. Cols, new("Nope", "ناموجود")], new[] { row }, 1);
        Assert.Null(withExtra.Rows[0]["Nope"]);
    }
}