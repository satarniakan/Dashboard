using Dashboard.Domain.Accounting;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>یک سطر گردش حساب مالیاتی — تخت و مستقل از navigation.</summary>
internal sealed class VatLine
{
    public DateTime Date { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

/// <summary>
/// اعلامیه ارزش افزوده: گردش و ماندهٔ ۲۳۰۰ (مالیات فروش) و ۱۳۵۰ (اعتبار مالیاتی خرید).
/// حجم سطرهای این دو حساب به‌ازای هر فروش/خرید فقط یک سطر است، پس خواندنِ کاملِ
/// بازه و تجمیع در حافظه (همان الگوی ProfitReportQuery چون GroupBy روی join
/// در EF ترجمه نمی‌شود) سبک است.
/// </summary>
public class VatReportQuery : IVatReportQuery
{
    private static readonly string[] VatCodes =
    {
        SystemAccountCodes.VatPayable,
        SystemAccountCodes.VatReceivable
    };

    private readonly AppDbContext _db;

    public VatReportQuery(AppDbContext db) => _db = db;

    public async Task<ReportTable> GetVatLinesAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Date", "تاریخ", ReportColumnKind.Date),
            new("EntryNumber", "شماره سند", ReportColumnKind.Text),
            new("AccountCode", "کد حساب", ReportColumnKind.Text),
            new("AccountName", "حساب", ReportColumnKind.Text),
            new("Description", "شرح", ReportColumnKind.Text),
            new("Debit", "بدهکار", ReportColumnKind.Money),
            new("Credit", "بستانکار", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q =
            from line in _db.JournalEntryLines.AsNoTracking()
            join acc in _db.Accounts.AsNoTracking() on line.AccountId equals acc.Id
            join entry in _db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where VatCodes.Contains(acc.Code)
            select new VatLine
            {
                Date = entry.EntryDate,
                EntryNumber = entry.EntryNumber,
                AccountCode = acc.Code,
                AccountName = acc.Name,
                // شرحِ سطر ملاک است؛ اگر سطر شرح نداشت، شرح خود سند
                Description = line.Description ?? entry.Description,
                Debit = line.DebitAmount,
                Credit = line.CreditAmount
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(l => l.Date >= from);
        }
        if (f.ToDate.HasValue)
        {
            // مرز بالا باز: تا ابتدای روزِ بعد، تا کل روزِ «تا» داخل بازه بماند
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(l => l.Date < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(l => l.Description.Contains(s)
                          || l.EntryNumber.Contains(s)
                          || l.AccountName.Contains(s));
        }

        // عمداً بدون Take: برش فقط در BuildPaged انجام می‌شود تا خروجی اکسل
        // «همهٔ» رکوردهای فیلترشده را داشته باشد (قرارداد نمایش ≠ خروجی)
        var lines = await q
            .OrderBy(l => l.Date).ThenBy(l => l.EntryNumber)
            .ToListAsync(ct);

        return ReportTableBuilder.BuildPaged(columns, lines, options);
    }

    public async Task<VatSummaryDto> GetVatSummaryAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var f = filter.Normalized();
        DateTime? toExclusive = f.ToDate.HasValue ? f.ToDate.Value.Date.AddDays(1) : null;

        var q =
            from line in _db.JournalEntryLines.AsNoTracking()
            join acc in _db.Accounts.AsNoTracking() on line.AccountId equals acc.Id
            join entry in _db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where VatCodes.Contains(acc.Code)
                 && (toExclusive == null || entry.EntryDate < toExclusive)
            select new
            {
                AccountCode = acc.Code,
                AccountName = acc.Name,
                EntryDate = entry.EntryDate,
                Debit = line.DebitAmount,
                Credit = line.CreditAmount
            };

        var allLines = await q.ToListAsync(ct);

        VatAccountPeriodSummary Sum(string code, string fallbackName)
        {
            var rows = allLines.Where(l => l.AccountCode == code).ToList();
            var name = rows.FirstOrDefault()?.AccountName ?? fallbackName;

            // «ابتدای دوره» فقط وقتی معنا دارد که از تاریخ داده شده باشد
            var openingRows = rows
                .Where(l => f.FromDate.HasValue && l.EntryDate < f.FromDate.Value.Date)
                .ToList();
            var periodRows = rows
                .Where(l => !f.FromDate.HasValue || l.EntryDate >= f.FromDate.Value.Date)
                .ToList();

            return new VatAccountPeriodSummary(
                code, name,
                openingRows.Sum(l => l.Debit), openingRows.Sum(l => l.Credit),
                periodRows.Sum(l => l.Debit), periodRows.Sum(l => l.Credit));
        }

        return new VatSummaryDto(
            Sum(SystemAccountCodes.VatPayable, "مالیات بر ارزش افزوده فروش"),
            Sum(SystemAccountCodes.VatReceivable, "اعتبار مالیاتی خرید"));
    }
}
