using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>سطر خروجِ بدون فروش (ضایعات یا مصرف داخلی)، تخت.</summary>
internal sealed class LossLine
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public bool IsScrap { get; set; }
    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }

    /// <summary>بهای تمام‌شدهٔ فعلی کالا، برای برآورد ارزشِ از‌دست‌رفته.</summary>
    public decimal UnitCost { get; set; }
}

/// <summary>
/// گزارش ضایعات و مصرف داخلی.
/// <para>
/// هر دو در یک گزارش جمع شده‌اند چون از دید کسب‌وکار یکی هستند: «خروجِ کالا بدون
/// فروش». جدا کردنشان فقط جدول را شلوغ می‌کند؛ ستون <c>Kind</c> تفکیک را نگه می‌دارد.
/// </para>
/// </summary>
public partial class PurchaseReportQuery
{
    /// <summary>ضایعات و حوالهٔ مصرف داخلی با هم.</summary>
    public async Task<ReportTable> GetStockLossAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("Kind", "نوع", ReportColumnKind.Text),
            new("DocCount", "تعداد سند", ReportColumnKind.Integer),
            new("TotalQty", "مقدار", ReportColumnKind.Number),
            new("UnitCost", "بهای واحد", ReportColumnKind.Money),
            new("LossValue", "ارزش از‌دست‌رفته", ReportColumnKind.Money),
            new("TopReason", "رایج‌ترین علت", ReportColumnKind.Text)
        };

        var f = filter.Normalized();
        var lines = new List<LossLine>();

        // ضایعات
        var scrapQ =
            from item in _db.ScrapRecordItems.AsNoTracking()
            join rec in _db.ScrapRecords.AsNoTracking() on item.ScrapRecordId equals rec.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join wh in _db.Warehouses.AsNoTracking() on rec.WarehouseId equals wh.Id
            select new LossLine
            {
                ProductId = prod.Id, Sku = prod.Sku, ProductName = prod.Name,
                Warehouse = wh.Name, IsScrap = true, Date = rec.RecordDate,
                Quantity = item.Quantity, Reason = rec.Reason, UnitCost = prod.CostPrice
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; scrapQ = scrapQ.Where(i => i.Date >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            scrapQ = scrapQ.Where(i => i.Date < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            scrapQ = scrapQ.Where(i => i.ProductName.Contains(s) || i.Sku.Contains(s));
        }
        lines.AddRange(await scrapQ.ToListAsync(ct));

        // حوالهٔ مصرف داخلی
        var issueQ =
            from item in _db.InternalIssueItems.AsNoTracking()
            join iss in _db.InternalIssues.AsNoTracking() on item.InternalIssueId equals iss.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join wh in _db.Warehouses.AsNoTracking() on iss.WarehouseId equals wh.Id
            select new LossLine
            {
                ProductId = prod.Id, Sku = prod.Sku, ProductName = prod.Name,
                Warehouse = wh.Name, IsScrap = false, Date = iss.IssueDate,
                Quantity = item.Quantity, Reason = iss.Purpose, UnitCost = prod.CostPrice
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; issueQ = issueQ.Where(i => i.Date >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            issueQ = issueQ.Where(i => i.Date < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            issueQ = issueQ.Where(i => i.ProductName.Contains(s) || i.Sku.Contains(s));
        }
        lines.AddRange(await issueQ.ToListAsync(ct));

        var items = lines
            .GroupBy(l => (l.ProductId, l.Warehouse, l.IsScrap))
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                return new
                {
                    Sku = g.First().Sku,
                    Name = g.First().ProductName,
                    Warehouse = g.Key.Warehouse,
                    Kind = g.Key.IsScrap ? "ضایعات" : "مصرف داخلی",
                    DocCount = g.Count(),
                    TotalQty = qty,
                    UnitCost = g.First().UnitCost,
                    LossValue = qty * g.First().UnitCost,
                    // رایج‌ترین علت: اگر همه یکی باشد، همان را نشان می‌دهد
                    TopReason = g.Select(x => x.Reason)
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .GroupBy(r => r!)
                        .OrderByDescending(rg => rg.Count())
                        .Select(rg => rg.Key)
                        .FirstOrDefault() ?? "—"
                };
            })
            .OrderByDescending(x => x.LossValue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
