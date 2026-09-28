using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>سطر سند حسابداری، تخت و مستقل از navigation.</summary>
internal sealed class JournalLine
{
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string EntryDescription { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

/// <summary>
/// گزارش‌های مالی: دفتر کل، گردش حساب‌ها، دریافت و پرداخت.
/// </summary>
public partial class FinancialReportQuery : IFinancialReportQuery
{
    private readonly AppDbContext _db;

    public FinancialReportQuery(AppDbContext db) => _db = db;

    /// <summary>
    /// دفتر کل: ماندهٔ هر حساب در بازه.
    /// <para>
    /// مانده = بدهکار − بستانکار. برای حساب‌های دارایی و هزینه مثبت است و برای
    /// حساب‌های بدهی و درآمد منفی — این طبیعیِ حسابداری است نه خطا.
    /// </para>
    /// <para>
    /// این گزارش <b>نقطهٔ کنترل</b> است: جمع بدهکار باید برابر جمع بستانکار باشد.
    /// اگر نبود، جایی سند نامتوازن ثبت شده.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetTrialBalanceAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Code", "کد حساب", ReportColumnKind.Text),
            new("Name", "نام حساب", ReportColumnKind.Text),
            new("EntryCount", "تعداد سطر", ReportColumnKind.Integer),
            new("TotalDebit", "جمع بدهکار", ReportColumnKind.Money),
            new("TotalCredit", "جمع بستانکار", ReportColumnKind.Money),
            new("Balance", "مانده", ReportColumnKind.Money)
        };

        var lines = await BaseJournalLines(filter, ct);

        var items = lines
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                return new
                {
                    Code = g.First().AccountCode,
                    Name = g.First().AccountName,
                    EntryCount = g.Count(),
                    TotalDebit = debit,
                    TotalCredit = credit,
                    Balance = debit - credit
                };
            })
            .OrderBy(x => x.Code)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>سطرهای پایهٔ سند حسابداری با فیلتر بازه و جست‌وجو.</summary>
    private async Task<List<JournalLine>> BaseJournalLines(ReportFilter filter, CancellationToken ct)
    {
        var f = filter.Normalized();

        var q =
            from line in _db.JournalEntryLines.AsNoTracking()
            join entry in _db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            join account in _db.Accounts.AsNoTracking() on line.AccountId equals account.Id
            select new JournalLine
            {
                EntryNumber = entry.EntryNumber,
                EntryDate = entry.EntryDate,
                EntryDescription = entry.Description,
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.Name,
                Debit = line.DebitAmount,
                Credit = line.CreditAmount
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(l => l.EntryDate >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(l => l.EntryDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(l => l.EntryNumber.Contains(s) || l.AccountName.Contains(s)
                             || l.AccountCode.Contains(s) || l.EntryDescription.Contains(s));
        }

        return await q.ToListAsync(ct);
    }
}
