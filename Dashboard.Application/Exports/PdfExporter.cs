using System.Globalization;
using System.Reflection;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Queries;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dashboard.Application.Exports;

/// <summary>
/// تبدیل <see cref="ReportTable"/> به PDF با چیدمان راست‌به‌چپ و فونت فارسی.
/// برخلاف اکسل، PDF برای چاپ یا بایگانی است و همیشه کل ردیف‌ها را می‌گیرد
/// (مسیر خروجی، بدون صفحه‌بندی).
/// </summary>
public interface IPdfExporter
{
    byte[] Export(ReportTable table, string title, string? subtitle = null);
}

public class PdfExporter : IPdfExporter
{
    /// <summary>رنگ‌های استاندارد گزارش — با تم روشن رابط کاربری هماهنگ است</summary>
    private const string HeaderBg = "#2c3e50";
    private const string RowAltBg = "#f5f7fa";
    private const string BorderColor = "#dde1e6";
    private const string MutedText = "#6c757d";
    private const string NegativeText = "#c0392b";

    private const string PersianFont = "Vazirmatn";
    private static int _configured;

    public byte[] Export(ReportTable table, string title, string? subtitle = null)
    {
        // QuestPDF تنظیماتش را سراسری (static) نگه می‌دارد؛ برای اینکه چند درخواست
        // هم‌زمان همدیگر را خراب نکنند، فقط یک‌بار تنظیم می‌شود.
        ConfigureOnce();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // گزارش‌های تحلیلی ستون زیاد دارند، پس افقی
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);

                // RTL روی خود صفحه اعمال می‌شود تا ترتیب ستون‌های جدول هم از
                // راست به چپ باشد (نه فقط متن). این در QuestPDF 2026 جایگزین
                // TextDirection قدیمی شده است.
                page.ContentFromRightToLeft();

                // تراز پیش‌فرض راست‌چین را ContentFromRightToLeft اعمال می‌کند،
                // پس اینجا فقط فونت و اندازه لازم است
                page.DefaultTextStyle(t => t
                    .FontFamily(PersianFont)
                    .FontSize(8));

                page.Header().Element(c => ComposeHeader(c, title, subtitle, table));
                page.Content().Element(c => ComposeTable(c, table));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ConfigureOnce()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1) return;

        QuestPDF.Settings.License = LicenseType.Community;
        // اگر فونت گلیفی نداشته باشد نباید کل سند بترکد؛ فقط همان متن هشدار می‌خورد
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

        // فونت فارسی سراسری ثبت می‌شود. بدون آن، متن به‌صورت حروف جدا از هم
        // چاپ می‌شود. نسخهٔ woff2 موجود در wwwroot برای PDF قابل استفاده نیست،
        // چون PDF به TTF نیاز دارد.
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(LoadFontStream("vazirmatn-regular.ttf"));
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(LoadFontStream("vazirmatn-500.ttf"));
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(LoadFontStream("vazirmatn-700.ttf"));
    }

    /// <summary>
    /// خواندن فونت وزیرمتن از منابع اسمبلی. بدون فونت فارسی، متن در PDF به‌صورت
    /// حروف جدا از هم و بدون شکل‌دهی چاپ می‌شود و ناخوانا است. نسخهٔ woff2 موجود در
    /// wwwroot برای PDF قابل استفاده نیست، چون PDF به TTF نیاز دارد.
    /// </summary>
    private static Stream LoadFontStream(string fileName)
    {
        var resource = $"Dashboard.Application.Assets.Fonts.{fileName}";
        var stream = typeof(PdfExporter).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
            throw new InvalidOperationException(
                $"فونت «{fileName}» در اسمبلی پیدا نشد. بررسی کنید که در «{resource}» به‌عنوان EmbeddedResource ثبت شده باشد.");
        return stream;
    }


    private static void ComposeHeader(
        IContainer container, string title, string? subtitle, ReportTable table)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(15).SemiBold().FontColor(HeaderBg);

            if (!string.IsNullOrWhiteSpace(subtitle))
                column.Item().PaddingTop(2).Text(subtitle).FontSize(9).FontColor(MutedText);

            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(BorderColor);

            // نوار اطلاعات: شرایط گزارش تا معلوم باشد این فایل با چه داده‌ای تهیه شده
            column.Item().PaddingTop(6)
                .Text(MetaText(table))
                .FontSize(7.5f).FontColor(MutedText);
        });
    }

    private static string MetaText(ReportTable table)
    {
        var count = table.TotalCount ?? table.Rows.Count;
        var text = $"تعداد رکورد: {count.ToString("N0", CultureInfo.InvariantCulture)}";

        // اگر گزارش صفحه‌بندی شده باشد، به کاربر می‌گوییم فایل کامل است
        if (table.Truncated)
            text += "  |  توجه: نمایش صفحه‌بندی شده، ولی این فایل شامل همهٔ رکوردهای فیلترشده است.";

        // Kind=Local در ToPersianDate اختلاف +۳:۳۰ را رد می‌کند؛ سرورِ UTC تاریخ را یک روز عقب می‌برد
        text += $"  |  تاریخ تهیه: {DateTime.UtcNow.ToPersianDate()}";
        return text;
    }

    private static void ComposeTable(IContainer container, ReportTable table)
    {
        if (table.Rows.Count == 0)
        {
            container.PaddingVertical(24).AlignCenter()
                .Text("داده‌ای با این فیلترها یافت نشد.")
                .FontColor(MutedText);
            return;
        }

        container.Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);   // شمارهٔ ردیف
                foreach (var col in table.Columns)
                    columns.RelativeColumn(RelativeWidth(col));
            });

            ComposeHeaderRow(t, table);

            for (var i = 0; i < table.Rows.Count; i++)
                ComposeDataRow(t, table, table.Rows[i], i);
        });
    }

    /// <summary>
    /// عرض نسبی هر ستون: ستون‌های متنی پهن‌تر و ستون‌های عددی باریک‌تر، تا
    /// ستون‌های زیاد در یک صفحه جا شوند.

    /// <summary>
    /// سرستون‌ها فقط با <c>table.Header</c> ساخته می‌شوند. QuestPDF خودش آن‌ها را
    /// در ابتدای هر صفحه تکرار می‌کند.
    /// <para>
    /// نکتهٔ مهم: اگر هم <c>table.Cell()</c> و هم <c>table.Header()</c> را صدا بزنیم،
    /// سرستون دو بار چاپ می‌شود — دقیقاً همان باگی که در اولین پیاده‌سازی رخ داد.
    /// </para>
    /// </summary>
    private static void ComposeHeaderRow(TableDescriptor table, ReportTable source)
    {
        table.Header(header =>
        {
            header.Cell().Element(HeaderCellStyle)
                .Text(t => { t.AlignCenter(); t.Span("#").FontColor("#ffffff"); });
            foreach (var col in source.Columns)
            {
                header.Cell().Element(HeaderCellStyle)
                    .Text(t => { AlignFor(col, t); t.Span(col.Title).FontColor("#ffffff"); });
            }
        });
    }

    private static void ComposeDataRow(
        TableDescriptor table, ReportTable source, IReadOnlyDictionary<string, object?> row, int index)
    {
        var isAlt = index % 2 == 1;

        table.Cell().Element(c => DataCellStyle(c, isAlt))
            .Text(t =>
            {
                t.AlignCenter();
                t.Span((index + 1).ToString(CultureInfo.InvariantCulture)).FontColor(MutedText);
            });

        foreach (var col in source.Columns)
        {
            var value = row.GetValueOrDefault(col.Key);
            table.Cell().Element(c => DataCellStyle(c, isAlt))
                .Text(FormatCell(col, value));
        }
    }

    private static IContainer HeaderCellStyle(IContainer c) => c
        .Background(HeaderBg)
        .PaddingVertical(5)
        .PaddingHorizontal(4)
        .BorderBottom(1).BorderColor(HeaderBg);

    private static IContainer DataCellStyle(IContainer c, bool isAlt) => c
        .Background(isAlt ? RowAltBg : "#ffffff")
        .PaddingVertical(3)
        .PaddingHorizontal(4)
        .BorderBottom(1).BorderColor(BorderColor);

    /// <summary>
    /// قالب‌بندی مقدار بر اساس نوع ستون. اعداد با جداکنندهٔ هزارگان و ارقام فارسی
    /// نوشته می‌شوند تا با بقیهٔ رابط کاربری یکدست باشد؛ منفی‌ها قرمز تا
    /// زیان‌ده بودن کالا فوراً به چشم بیاید.
    /// </summary>
    private static string FormatCell(ReportColumn col, object? value)
    {
        if (value is null or DBNull) return string.Empty;

        switch (col.Kind)
        {
            case ReportColumnKind.Money:
            case ReportColumnKind.Number:
            case ReportColumnKind.Integer:
            {
                if (!TryToDecimal(value, out var num)) return value.ToString() ?? string.Empty;

                var text = col.Kind == ReportColumnKind.Integer
                    ? ((long)num).ToString("N0", CultureInfo.InvariantCulture)
                    : num.ToString("N0", CultureInfo.InvariantCulture);

                // ارقام فارسی: خواندن اعداد روی گزارش چاپی بسیار راحت‌تر است
                return ToPersianDigits(num < 0 ? $"({text})" : text);
            }

            case ReportColumnKind.Date:
                return value is DateTime dt ? ToPersianDigits(dt.ToPersianDate()) : value.ToString() ?? string.Empty;

            default:
                return value.ToString() ?? string.Empty;
        }
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal d: result = d; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case double db: result = (decimal)db; return true;
            case float f: result = (decimal)f; return true;
            default:
                return decimal.TryParse(value.ToString(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out result);
        }
    }

    private static string ToPersianDigits(string input)
    {
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
            builder.Append(char.IsDigit(c) ? persian[c - '0'] : c);
        return builder.ToString();
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(0.5f).LineColor(BorderColor);
            column.Item().PaddingTop(6).AlignLeft()
                .Text(t =>
                {
                    t.CurrentPageNumber().FontSize(7.5f).FontColor(MutedText);
                    t.Span(" از ").FontSize(7.5f).FontColor(MutedText);
                    t.TotalPages().FontSize(7.5f).FontColor(MutedText);
                });
        });
    }

    /// </summary>
    /// <summary>
    /// ستون‌های عددی چپ‌چین می‌شوند (رقم‌ها راحت‌تر اسکن می‌شوند) و بقیه راست‌چین.
    /// </summary>
    /// <summary>
    /// ستون‌های عددی چپ‌چین می‌شوند (رقم‌ها راحت‌تر اسکن می‌شوند) و بقیه راست‌چین.
    /// با متدهای AlignLeft/AlignRight انجام می‌شود تا نیازی به نام‌بردن enum نباشد.
    /// </summary>
    private static void AlignFor(ReportColumn col, QuestPDF.Fluent.TextDescriptor t)
    {
        if (col.Kind is ReportColumnKind.Money or ReportColumnKind.Number or ReportColumnKind.Integer)
            t.AlignLeft();
        else
            t.AlignRight();
    }

    private static float RelativeWidth(ReportColumn col) => col.Kind switch
    {
        ReportColumnKind.Text => 2.2f,
        ReportColumnKind.Date => 1.6f,
        ReportColumnKind.Integer => 1.1f,
        ReportColumnKind.Number => 1.2f,
        ReportColumnKind.Money => 1.5f,
        _ => 1.5f
    };
}
