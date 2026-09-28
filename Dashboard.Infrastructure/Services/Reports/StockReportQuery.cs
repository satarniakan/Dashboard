using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>ردیف تخت موجودی، مستقل از navigation.</summary>
internal sealed class StockRow
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// گزارش وضعیت لحظه‌ای موجودی.
/// <para>
/// تفاوت با <c>SalesOperationsReportQuery</c>: آن‌ها تاریخچه را نشان می‌دهند
/// (بازه‌ای از گردش)، ولی اینجا فقط «الان» گزارش می‌شود. به همین دلیل فیلتر تاریخ
/// عمداً نادیده گرفته می‌شود — اعمال‌کردنش روی داده‌ای که تاریخ ندارد فقط
/// کاربر را گمراه می‌کند.
/// </para>
/// </summary>
public class StockReportQuery : IStockReportQuery
{
    private readonly AppDbContext _db;

    public StockReportQuery(AppDbContext db) => _db = db;

    /// <summary>موجودی فعلی به تفکیک کالا و انبار.</summary>
    public async Task<ReportTable> GetStockOnHandAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("OnHand", "موجودی", ReportColumnKind.Number),
            new("Reserved", "رزرو‌شده", ReportColumnKind.Number),
            new("Available", "قابل‌فروش", ReportColumnKind.Number),
            new("ReorderPoint", "نقطهٔ سفارش", ReportColumnKind.Number),
            new("UnitCost", "بهای واحد", ReportColumnKind.Money),
            new("StockValue", "ارزش موجودی", ReportColumnKind.Money)
        };

        var items = await BuildRowsAsync(filter, ct);

        return ReportTableBuilder.BuildPaged(
            columns,
            items
                .Select(r => new
                {
                    r.Sku,
                    r.Name,
                    r.Warehouse,
                    OnHand = r.OnHand,
                    Reserved = r.Reserved,
                    Available = r.Available,
                    ReorderPoint = r.ReorderPoint,
                    UnitCost = r.UnitCost,
                    StockValue = r.Available * r.UnitCost
                })
                .OrderByDescending(x => x.StockValue)
                .ToList(),
            options);
    }

    /// <summary>
    /// کالاهایی که موجودی قابل‌فروش‌شان به نقطهٔ سفارش رسیده یا زیر آن است.
    /// <para>
    /// مقایسه با «قابل‌فروش» انجام می‌شود نه موجودی کل: موجودیِ رزرو‌شده برای
    /// سفارش‌های در جریان کنار گذاشته شده و اگر آن را هم حساب کنیم، کالایی را
    /// «آمادهٔ خرید» می‌بینیم که در واقع نیست.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetBelowReorderPointAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("Available", "قابل‌فروش", ReportColumnKind.Number),
            new("ReorderPoint", "نقطهٔ سفارش", ReportColumnKind.Number),
            new("Shortage", "کمبود", ReportColumnKind.Number),
            new("SuggestedOrder", "سفارش پیشنهادی", ReportColumnKind.Number),
            new("UnitCost", "بهای واحد", ReportColumnKind.Money),
            new("EstimatedCost", "هزینهٔ تخمینی", ReportColumnKind.Money)
        };

        var rows = await BuildRowsAsync(filter, ct);

        var below = rows
            .Where(r => r.Available <= r.ReorderPoint)
            .Select(r =>
            {
                var shortage = Math.Max(0, r.ReorderPoint - r.Available);
                // سفارش پیشنهادی تا رسیدن به دو برابر نقطهٔ سفارش
                var suggested = Math.Max(0, (r.ReorderPoint * 2) - r.Available);
                return new
                {
                    r.Sku,
                    r.Name,
                    r.Warehouse,
                    r.Available,
                    r.ReorderPoint,
                    Shortage = shortage,
                    SuggestedOrder = suggested,
                    r.UnitCost,
                    EstimatedCost = suggested * r.UnitCost
                };
            })
            .OrderByDescending(x => x.Shortage)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, below, options);
    }

    /// <summary>
    /// سطرهای مشترکِ دو گزارش: موجودی هر کالا در هر انبار، همراه بهای تمام‌شده.
    /// بهای از <c>Product</c> خوانده می‌شود چون همان میانگین موزونِ نگه‌داری‌شده
    /// است و برای موجودیِ فعلی معتبرتر از بهای یک تراکنش قدیمی است.
    /// </summary>
    private async Task<List<StockRow>> BuildRowsAsync(ReportFilter filter, CancellationToken ct)
    {
        var f = filter.Normalized();

        var q =
            from level in _db.StockLevels.AsNoTracking()
            join prod in _db.Products.AsNoTracking() on level.ProductId equals prod.Id
            join wh in _db.Warehouses.AsNoTracking() on level.WarehouseId equals wh.Id
            select new StockRow
            {
                Sku = prod.Sku,
                Name = prod.Name,
                Warehouse = wh.Name,
                OnHand = level.QuantityOnHand,
                Reserved = level.ReservedQuantity,
                Available = level.QuantityOnHand - level.ReservedQuantity,
                ReorderPoint = prod.ReorderPoint,
                UnitCost = prod.CostPrice
            };

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(r => r.Name.Contains(s) || r.Sku.Contains(s));
        }

        return await q.ToListAsync(ct);
    }
}
