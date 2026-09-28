using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>گردش حساب‌های نقدی، دریافت‌ها، پرداخت‌ها و ممیزی.</summary>
public partial class FinancialReportQuery
{
    /// <summary>
    /// گردش حساب‌های صندوق و بانک — برای تطبیق با گردش واقعی.
    /// <para>
    /// فقط حساب‌های نقدی می‌آیند: حساب‌هایی که گردششان را بیرون از سیستم هم
    /// می‌بینیم و باید دستی تطبیق داده شوند. درآمد و هزینه گردش نقدی ندارند.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetAccountTransactionsAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("EntryNumber", "شمارهٔ سند", ReportColumnKind.Text),
            new("EntryDate", "تاریخ", ReportColumnKind.Date),
            new("Account", "حساب", ReportColumnKind.Text),
            new("Description", "شرح", ReportColumnKind.Text),
            new("Debit", "بدهکار", ReportColumnKind.Money),
            new("Credit", "بستانکار", ReportColumnKind.Money)
        };

        var cashTypes = new[]
        {
            Dashboard.Domain.Enums.FinancialAccountType.Cash,
            Dashboard.Domain.Enums.FinancialAccountType.Bank
        };
        var cashAccountIds = await _db.FinancialAccounts.AsNoTracking()
            .Where(f => f.IsActive && cashTypes.Contains(f.Type))
            .Select(f => f.AccountId)
            .ToListAsync(ct);

        var lines = await BaseJournalLines(filter, ct);

        var items = lines
            .Where(l => cashAccountIds.Contains(l.AccountId))
            .Select(l => new
            {
                l.EntryNumber,
                l.EntryDate,
                Account = $"{l.AccountCode} — {l.AccountName}",
                // اگر سند شرح نداشت، نام حساب جای خالی را پر می‌کند
                Description = string.IsNullOrWhiteSpace(l.EntryDescription)
                    ? l.AccountName
                    : l.EntryDescription,
                Debit = l.Debit,
                Credit = l.Credit
            })
            .OrderByDescending(x => x.EntryDate)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>دریافت‌ها از مشتریان، به تفکیک روش پرداخت.</summary>
    public async Task<ReportTable> GetCustomerReceiptsAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("ReceiptNumber", "شمارهٔ دریافت", ReportColumnKind.Text),
            new("ReceiptDate", "تاریخ", ReportColumnKind.Date),
            new("Customer", "مشتری", ReportColumnKind.Text),
            new("Account", "حساب", ReportColumnKind.Text),
            new("Method", "روش", ReportColumnKind.Text),
            new("Amount", "مبلغ", ReportColumnKind.Money),
            new("ChequeNumber", "شمارهٔ چک", ReportColumnKind.Text)
        };

        var f = filter.Normalized();

        var q =
            from r in _db.CustomerReceipts.AsNoTracking()
            join cust in _db.Customers.AsNoTracking() on r.CustomerId equals cust.Id into custGroup
            from cust in custGroup.DefaultIfEmpty()
            join acc in _db.FinancialAccounts.AsNoTracking() on r.FinancialAccountId equals acc.Id
            select new
            {
                r.ReceiptNumber, r.ReceiptDate, r.Amount, r.ChequeNumber,
                Customer = cust != null ? cust.Name : "مشتری متفرقه",
                acc.Name,
                Method = r.Method.ToString()
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.ReceiptDate >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.ReceiptDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.Customer.Contains(s) || i.ReceiptNumber.Contains(s));
        }

        var rows = await q.OrderByDescending(i => i.ReceiptDate).ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.ReceiptNumber, r.ReceiptDate, r.Customer, r.Name, r.Method, r.Amount, r.ChequeNumber
        }).ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>پرداخت‌ها به تأمین‌کنندگان، به تفکیک روش پرداخت.</summary>
    public async Task<ReportTable> GetSupplierPaymentsAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("PaymentNumber", "شمارهٔ پرداخت", ReportColumnKind.Text),
            new("PaymentDate", "تاریخ", ReportColumnKind.Date),
            new("Supplier", "تأمین‌کننده", ReportColumnKind.Text),
            new("Account", "حساب", ReportColumnKind.Text),
            new("Method", "روش", ReportColumnKind.Text),
            new("Amount", "مبلغ", ReportColumnKind.Money),
            new("ChequeNumber", "شمارهٔ چک", ReportColumnKind.Text)
        };

        var f = filter.Normalized();

        var q =
            from p in _db.SupplierPayments.AsNoTracking()
            join sup in _db.Suppliers.AsNoTracking() on p.SupplierId equals sup.Id
            join acc in _db.FinancialAccounts.AsNoTracking() on p.FinancialAccountId equals acc.Id
            select new
            {
                p.PaymentNumber, p.PaymentDate, p.Amount, p.ChequeNumber,
                Supplier = sup.Name,
                acc.Name,
                Method = p.Method.ToString()
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.PaymentDate >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.PaymentDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.Supplier.Contains(s) || i.PaymentNumber.Contains(s));
        }

        var rows = await q.OrderByDescending(i => i.PaymentDate).ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.PaymentNumber, r.PaymentDate, r.Supplier, r.Name, r.Method, r.Amount, r.ChequeNumber
        }).ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
