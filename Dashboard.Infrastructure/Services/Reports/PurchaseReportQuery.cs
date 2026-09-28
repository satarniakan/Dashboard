using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>سطر خرید، تخت و مستقل از navigation.</summary>
internal sealed class PurchaseLine
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int ReceiptId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// گزارش‌های تأمین و خرید (بخش خریدِ فاز ۲).
/// مکملِ <see cref="StockReportQuery"/>: آن‌ها وضعیت لحظه‌ای را نشان می‌دهند،
/// این‌ها تاریخچهٔ خرید و بهای را.
/// </summary>
public partial class PurchaseReportQuery : IPurchaseReportQuery
{
    private readonly AppDbContext _db;

    public PurchaseReportQuery(AppDbContext db) => _db = db;

    /// <summary>رسیدهای خرید به تفکیک تأمین‌کننده.</summary>
    public async Task<ReportTable> GetPurchaseBySupplierAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Supplier", "تأمین‌کننده", ReportColumnKind.Text),
            new("ReceiptCount", "تعداد رسید", ReportColumnKind.Integer),
            new("ProductCount", "کالای مختلف", ReportColumnKind.Integer),
            new("TotalQty", "مقدار کل", ReportColumnKind.Number),
            new("TotalCost", "مبلغ کل", ReportColumnKind.Money),
            new("AvgUnitCost", "میانگین بهای واحد", ReportColumnKind.Money),
            new("FirstDate", "اولین خرید", ReportColumnKind.Date),
            new("LastDate", "آخرین خرید", ReportColumnKind.Date)
        };

        var lines = await BasePurchaseLines(filter, ct);

        var items = lines
            .GroupBy(l => l.SupplierId)
            .Select(g =>
            {
                var totalQty = g.Sum(x => x.Quantity);
                var totalCost = g.Sum(x => x.Quantity * x.UnitCost);
                return new
                {
                    Supplier = g.First().SupplierName,
                    ReceiptCount = g.Select(x => x.ReceiptId).Distinct().Count(),
                    ProductCount = g.Select(x => x.ProductId).Distinct().Count(),
                    TotalQty = totalQty,
                    TotalCost = totalCost,
                    // میانگین وزنی: تقسیم بر مجموع مقدار، نه تعداد سطر
                    AvgUnitCost = totalQty == 0 ? 0 : Math.Round(totalCost / totalQty, 0),
                    FirstDate = g.Min(x => x.ReceiptDate),
                    LastDate = g.Max(x => x.ReceiptDate)
                };
            })
            .OrderByDescending(x => x.TotalCost)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// بهای تمام‌شدهٔ واقعی هر کالا در بازه، از روی رسیدهای خرید.
    /// <para>
    /// این گزارش برای <b>راستی‌آزمایی</b> است: عددی که می‌دهد (میانگین موزونِ
    /// محاسبه‌شده در لحظهٔ خرید) باید با چیزی که گزارش سود نشان می‌دهد بخواند.
    /// اگر اختلاف داشتند، یعنی محاسبهٔ بها جایی اشتباه شده.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetActualCostByProductAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("CurrentCost", "بهای فعلی کالا", ReportColumnKind.Money),
            new("WeightedAvg", "میانگین موزون خرید", ReportColumnKind.Money),
            new("Difference", "اختلاف", ReportColumnKind.Money),
            new("TotalQty", "مقدار خریداری‌شده", ReportColumnKind.Number),
            new("LastPurchase", "آخرین خرید", ReportColumnKind.Date),
            new("LastUnitCost", "بهای آخرین خرید", ReportColumnKind.Money)
        };

        var lines = await BasePurchaseLines(filter, ct);

        // بهای فعلی کالا جدا خوانده می‌شود: برای مقایسه لازم است و محاسبهٔ
        // GroupBy روی کوئری join‌شده در EF ترجمه نمی‌شود
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var currentCosts = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CostPrice })
            .ToListAsync(ct);
        var costById = currentCosts.ToDictionary(c => c.Id, c => c.CostPrice);

        var items = lines
            .GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var totalQty = g.Sum(x => x.Quantity);
                var totalCost = g.Sum(x => x.Quantity * x.UnitCost);
                // فرمول میانگین موزون روی همین بازه (همان قاعدهٔ StockService)
                var weighted = totalQty == 0 ? 0 : Math.Round(totalCost / totalQty, 0);
                var last = g.OrderByDescending(x => x.ReceiptDate).First();
                var current = costById.GetValueOrDefault(g.Key);

                return new
                {
                    Sku = g.First().Sku,
                    Name = g.First().ProductName,
                    CurrentCost = current,
                    WeightedAvg = weighted,
                    Difference = current - weighted,
                    TotalQty = totalQty,
                    LastPurchase = last.ReceiptDate,
                    LastUnitCost = last.UnitCost
                };
            })
            // فقط مغایرت‌های معنادار: اختلافِ کمتر از ۱۰ ریال گرد کردن است نه خطا
            .Where(x => Math.Abs(x.Difference) >= 10)
            .OrderByDescending(x => Math.Abs(x.Difference))
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// سطرهای پایهٔ خرید: هر سطر رسید، تخت و مستقل از navigation.
    /// گروه‌بندی در حافظه انجام می‌شود چون GroupBy روی کوئری join‌شده در EF
    /// Core 10 ترجمه نمی‌شود.
    /// </summary>
    private async Task<List<PurchaseLine>> BasePurchaseLines(ReportFilter filter, CancellationToken ct)
    {
        var f = filter.Normalized();

        var q =
            from item in _db.PurchaseReceiptItems.AsNoTracking()
            join receipt in _db.PurchaseReceipts.AsNoTracking()
                on item.PurchaseReceiptId equals receipt.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join sup in _db.Suppliers.AsNoTracking() on receipt.SupplierId equals sup.Id
            select new PurchaseLine
            {
                SupplierId = sup.Id,
                SupplierName = sup.Name,
                ProductId = prod.Id,
                Sku = prod.Sku,
                ProductName = prod.Name,
                ReceiptId = receipt.Id,
                ReceiptDate = receipt.ReceiptDate,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.ReceiptDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.ReceiptDate < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.SupplierName.Contains(s) || i.ProductName.Contains(s) || i.Sku.Contains(s));
        }

        return await q.ToListAsync(ct);
    }
}
