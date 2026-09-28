using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>انتقال بین انبارها و مغایرت انبارگردانی.</summary>
public partial class PurchaseReportQuery
{
    /// <summary>انتقال بین انبارها: کالا از کدام انبار به کدام انبار رفت.</summary>
    public async Task<ReportTable> GetWarehouseTransfersAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("FromWarehouse", "از انبار", ReportColumnKind.Text),
            new("ToWarehouse", "به انبار", ReportColumnKind.Text),
            new("TransferCount", "تعداد سطر", ReportColumnKind.Integer),
            new("TotalQty", "مقدار منتقل‌شده", ReportColumnKind.Number)
        };

        var f = filter.Normalized();

        var q =
            from item in _db.StockTransferItems.AsNoTracking()
            join tr in _db.StockTransfers.AsNoTracking() on item.StockTransferId equals tr.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join fromWh in _db.Warehouses.AsNoTracking() on tr.SourceWarehouseId equals fromWh.Id
            join toWh in _db.Warehouses.AsNoTracking() on tr.DestinationWarehouseId equals toWh.Id
            select new
            {
                prod.Id, prod.Sku, prod.Name,
                FromWarehouse = fromWh.Name,
                ToWarehouse = toWh.Name,
                tr.TransferDate,
                item.Quantity
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.TransferDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.TransferDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.Name.Contains(s) || i.Sku.Contains(s));
        }

        var rows = await q.ToListAsync(ct);

        var items = rows
            .GroupBy(r => (r.Id, r.FromWarehouse, r.ToWarehouse))
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    Sku = first.Sku,
                    Name = first.Name,
                    FromWarehouse = g.Key.FromWarehouse,
                    ToWarehouse = g.Key.ToWarehouse,
                    TransferCount = g.Count(),
                    TotalQty = g.Sum(x => x.Quantity)
                };
            })
            .OrderByDescending(x => x.TotalQty)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// مغایرت انبارگردانی: موجودی سیستمی در برابر شمارش واقعی.
    /// <para>
    /// فقط شمارش‌های <b>بسته‌شده</b> می‌آیند. شمارشِ باز هنوز در حال انجام است و
    /// «اختلاف» آن عددِ نهایی نیست — گزارشِ ناقص می‌دهد و کاربر را گمراه می‌کند.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetStockCountVarianceAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("CountNumber", "شمارهٔ انبارگردانی", ReportColumnKind.Text),
            new("CountDate", "تاریخ", ReportColumnKind.Date),
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("SystemQty", "موجودی سیستمی", ReportColumnKind.Number),
            new("CountedQty", "شمارش واقعی", ReportColumnKind.Number),
            new("Discrepancy", "اختلاف", ReportColumnKind.Number),
            new("UnitCost", "بهای واحد", ReportColumnKind.Money),
            new("Value", "ارزش اختلاف", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q =
            from item in _db.StockCountItems.AsNoTracking()
            join count in _db.StockCounts.AsNoTracking() on item.StockCountId equals count.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join wh in _db.Warehouses.AsNoTracking() on count.WarehouseId equals wh.Id
            where count.Status == Dashboard.Domain.Enums.StockCountStatus.Closed
            select new
            {
                count.CountNumber, count.CountDate,
                Warehouse = wh.Name,
                prod.Sku, prod.Name,
                item.SystemQuantity,
                item.CountedQuantity,
                UnitCost = prod.CostPrice
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.CountDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.CountDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.Name.Contains(s) || i.Sku.Contains(s) || i.CountNumber.Contains(s));
        }

        var rows = await q.ToListAsync(ct);

        var items = rows
            .Select(r =>
            {
                var diff = r.CountedQuantity - r.SystemQuantity;
                return new
                {
                    r.CountNumber, r.CountDate, r.Warehouse, r.Sku, r.Name,
                    SystemQty = r.SystemQuantity,
                    CountedQty = r.CountedQuantity,
                    Discrepancy = diff,
                    r.UnitCost,
                    Value = diff * r.UnitCost
                };
            })
            // فقط مغایرت‌های واقعی؛ سطرهای منطبق برای کاربر نویزند
            .Where(x => x.Discrepancy != 0)
            .OrderByDescending(x => Math.Abs(x.Value))
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
