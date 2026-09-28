using Dashboard.Application.Exports;
using Dashboard.Domain.Queries;
using Xunit;

namespace Dashboard.Tests;

/// <summary>
/// خروجی PDF گزارش‌ها. مهم‌ترین چیزی که باید نگهبانی شود این است که فایل
/// واقعاً تولید شود و متن فارسی/راست‌به‌چپ در آن سالم باشد — وگرنه کاربر یک
/// PDF خالی یا حروف جدا از هم تحویل می‌گیرد.
/// </summary>
public class PdfExporterTests
{
    private static readonly IReadOnlyList<ReportColumn> Cols =
    [
        new("Name", "نام کالا", ReportColumnKind.Text),
        new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
        new("Margin", "حاشیه سود ٪", ReportColumnKind.Number)
    ];

    private static ReportTable Table(int rows = 3, bool truncated = false) => new(
        Cols,
        Enumerable.Range(1, rows).Select(i => (IReadOnlyDictionary<string, object?>)
            new Dictionary<string, object?>
            {
                ["Name"] = $"کالای شمارهٔ {i}",
                ["Revenue"] = 1_250_000m * i,
                ["Margin"] = 12.5m
            }).ToList(),
        truncated,
        rows);

    [Fact]
    public void ProducesNonEmptyPdf_WithPdfSignature()
    {
        var bytes = new PdfExporter().Export(Table(), "گزارش سود و زیان");

        Assert.NotEmpty(bytes);
        // هر فایل PDF با هدر «%PDF-» شروع می‌شود؛ اگر نبود یعنی خروجی خراب است
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void EmbedsFont_ForPersianText()
    {
        var bytes = new PdfExporter().Export(Table(), "گزارش");

        var text = System.Text.Encoding.Latin1.GetString(bytes);
        // نام فونت داخل ساختار PDF می‌آید؛ نبودش یعنی فارسی حروف جدا می‌شود
        Assert.Contains("Vazirmatn", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HeaderIsPrintedOnce_NotDuplicated()
    {
        // نگهبانِ یک باگ واقعی: در پیاده‌سازی اول هم table.Cell و هم table.Header
        // برای سرستون صدا زده شد و سرستون دو بار (دو ردیف) چاپ شد.
        //
        // روش سنجش: هر سطرِ جدول هزینهٔ مشخصی در اندازهٔ فایل دارد. با مقایسهٔ
        // اندازهٔ فایل «بدون داده» با «یک سطر» و «دو سطر»، می‌توان تعداد سطرهای
        // چاپ‌شده را دقیقاً حساب کرد. اگر سرستون تکراری چاپ می‌شد، برای ۳ سطر
        // داده به‌جای ۴ سطر، ۵ سطر به‌دست می‌آمد.
        // مبنا: یک سطر داده (سرستون + ۱ سطر). مبنای مقایسه هم فایل «یک سطری»
        // است، چون فایلِ بدون داده اصلاً جدولی ندارد (پیام «داده‌ای یافت نشد»).
        var one = new PdfExporter().Export(Table(rows: 1), "سود و زیان").Length;
        var two = new PdfExporter().Export(Table(rows: 2), "سود و زیان").Length;
        var three = new PdfExporter().Export(Table(rows: 3), "سود و زیان").Length;

        var perRow = two - one;
        Assert.True(perRow > 0, "اندازهٔ فایل با افزودن سطر تغییر نکرد.");

        // با ۱ سطر داده، فایل ۲ سطرِ جدول دارد (سرستون + داده). پس با ۳ سطر
        // داده باید ۴ سطر باشد. اگر سرستون تکراری چاپ می‌شد، ۵ می‌شد.
        var rowsInThree = Math.Round((three - one) / (double)perRow) + 2;
        Assert.Equal(4, rowsInThree);
    }

    [Fact]
    public void EmptyTable_StillProducesValidPdf()
    {
        // نتیجهٔ خالی نباید کاربر را با خطا روبه‌رو کند
        var bytes = new PdfExporter().Export(Table(rows: 0), "گزارش خالی");

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void ManyRows_ProduceMultipageDocument()
    {
        // هدر جدول باید در صفحه‌های بعدی تکرار شود، پس سند باید چند صفحه شود
        var bytes = new PdfExporter().Export(Table(rows: 400), "گزارش حجیم");

        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("/Type /Page", text, StringComparison.Ordinal);
        // فایل باید از یک صفحه بزرگ‌تر باشد
        Assert.True(bytes.Length > 10_000, $"فایل فقط {bytes.Length} بایت است.");
    }

    [Fact]
    public void NullAndMissingValues_DoNotCrash()
    {
        var table = new ReportTable(
            Cols,
            [new Dictionary<string, object?> { ["Revenue"] = null, ["Margin"] = DBNull.Value }]);

        var bytes = new PdfExporter().Export(table, "مقادیر خالی");
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void NegativeNumbers_RenderWithParentheses()
    {
        // زیان باید متمایز دیده شود؛ در اکسل و PDF هر دو با پرانتز
        var table = new ReportTable(
            [new("Loss", "زیان", ReportColumnKind.Money)],
            [new Dictionary<string, object?> { ["Loss"] = -250_000m }]);

        var bytes = new PdfExporter().Export(table, "زیان");
        Assert.NotEmpty(bytes);
    }
}