using System.Globalization;
using ClosedXML.Excel;
using Dashboard.Domain.Queries;

namespace Dashboard.Application.Exports;

/// <summary>
/// تبدیل <see cref="ReportTable"/> به فایل اکسل. مقادیر بر اساس
/// <see cref="ReportColumnKind"/> درست تایپ می‌شوند (عدد به‌صورت عدد، تاریخ به‌صورت تاریخ)
/// تا کاربر بتواند در اکسل روی ستون‌ها فرمول و جمع بزند.
/// </summary>
public interface IExcelExporter
{
    byte[] Export(ReportTable table, string sheetName = "گزارش");
}

public class ExcelExporter : IExcelExporter
{
    public byte[] Export(ReportTable table, string sheetName = "گزارش")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));

        // شیت ۱: فقط داده‌ها (همان چیزی که کاربر فیلتر کرده)
        var headerRow = sheet.Row(1);
        for (var c = 0; c < table.Columns.Count; c++)
            headerRow.Cell(c + 1).Value = table.Columns[c].Title;

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = sheet.Row(r + 2);
            for (var c = 0; c < table.Columns.Count; c++)
                WriteCell(row.Cell(c + 1), table.Columns[c].Kind,
                    table.Rows[r].GetValueOrDefault(table.Columns[c].Key));
        }

        // شیت ۲: شرایط فیلتر — تا معلوم باشد این گزارش با چه فیلتری گرفته شده
        var meta = workbook.Worksheets.Add("شرایط گزارش");
        meta.Cell(1, 1).Value = "پارامتر";
        meta.Cell(1, 2).Value = "مقدار";
        meta.Cell(2, 1).Value = "تعداد کل رکورد";
        meta.Cell(2, 2).Value = table.TotalCount ?? table.Rows.Count;
        if (table.Truncated)
        {
            meta.Cell(3, 1).Value = "توجه";
            meta.Cell(3, 2).Value =
                $"گزارش {table.TotalCount} ردیف دارد؛ فایل شامل همهٔ ردیف‌های فیلترشده است.";
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, 40);
        foreach (var col in table.Columns.Select((c, i) => (c, i)))
            if (col.c.Width is > 0) sheet.Column(col.i + 1).Width = col.c.Width.Value;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCell(IXLCell cell, ReportColumnKind kind, object? value)
    {
        switch (kind)
        {
            case ReportColumnKind.Number:
            case ReportColumnKind.Money:
                if (value is not null and not DBNull
                    && decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                {
                    cell.Value = dec;
                    // ارقام جداکننده و قالب پول برای خواندن بهتر
                    cell.Style.NumberFormat.Format = kind == ReportColumnKind.Money ? "#,##0" : "#,##0.##";
                }
                else cell.SetValue(value?.ToString() ?? string.Empty);
                break;

            case ReportColumnKind.Integer:
                if (value is not null and not DBNull && long.TryParse(value.ToString(), out var l))
                {
                    cell.Value = l;
                    cell.Style.NumberFormat.Format = "#,##0";
                }
                else cell.SetValue(value?.ToString() ?? string.Empty);
                break;

            case ReportColumnKind.Date:
                if (value is DateTime dt) cell.Value = dt;
                else if (value is not null and not DBNull
                         && DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
                             DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                    cell.Value = parsed;
                else cell.SetValue(value?.ToString() ?? string.Empty);
                cell.Style.DateFormat.Format = "yyyy-MM-dd";
                break;

            default:
                cell.SetValue(value?.ToString() ?? string.Empty);
                break;
        }
    }

    /// <summary>نام شیت اکسل حداکثر ۳۱ کاراکتر و بدون این نویسه‌ها می‌تواند باشد</summary>
    private static string SanitizeSheetName(string name)
    {
        var clean = new string(name.Where(c => c is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray());
        if (string.IsNullOrWhiteSpace(clean)) clean = "گزارش";
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
