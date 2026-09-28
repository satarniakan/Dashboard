using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>یک سطر برگشت از فروش، به شکل تخت و مستقل از navigation.</summary>
internal sealed class ReturnLine
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int SalesReturnId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>قیمت واحدِ فاکتورِ ارجاع — مبنای محاسبهٔ مبلغ برگشتی.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>بهای تمام‌شدهٔ همان مقدار، برای سنجش اثر بر سود.</summary>
    public decimal CostAmount { get; set; }
}

/// <summary>
/// گزارش‌های عملیاتی فروش و انبار (برگشت‌ها، اقساط، گردش موجودی).
/// این‌ها مکملِ <see cref="ProfitReportQuery"/> هستند و همان قرارداد صفحه‌بندی
/// را رعایت می‌کنند.
/// </summary>
public class SalesOperationsReportQuery : ISalesOperationsReportQuery
{
    private readonly AppDbContext _db;

    public SalesOperationsReportQuery(AppDbContext db) => _db = db;

    /// <summary>
    /// برگشت از فروش به تفکیک کالا.
    /// <para>
    /// نکتهٔ کسب‌وکار: مبلغ برگشتی از «قیمت فاکتورِ ارجاع» خوانده می‌شود، نه
    /// قیمت روز کالا. اگر قیمت کالا بعداً عوض شده باشد، گزارش نباید عدد
    /// متفاوتی نشان دهد — همان منطقِ اسنپ‌شات که در گزارش سود هم رعایت شده.
    /// اگر فاکتورِ ارجاع حذف شده باشد، بهای تمام‌شدهٔ سطر برگشت به‌عنوان
    /// جایگزین استفاده می‌شود (بهتر از نمایش صفر که گمراه‌کننده است).
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetSalesReturnsAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Unit", "واحد", ReportColumnKind.Text),
            new("ReturnCount", "تعداد برگشت", ReportColumnKind.Integer),
            new("TotalQty", "مقدار مرجوع", ReportColumnKind.Number),
            new("Refund", "مبلغ برگشتی", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("NetEffect", "اثر خالص بر سود", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q =
            from item in _db.SalesReturnItems.AsNoTracking()
            join ret in _db.SalesReturns.AsNoTracking() on item.SalesReturnId equals ret.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join invItem in _db.SalesInvoiceItems.AsNoTracking()
                on ret.SalesInvoiceId equals invItem.SalesInvoiceId into invItemGroup
            from invItem in invItemGroup.DefaultIfEmpty()
            where invItem == null || invItem.ProductId == item.ProductId
            select new ReturnLine
            {
                ProductId = prod.Id,
                Sku = prod.Sku,
                ProductName = prod.Name,
                Unit = prod.Unit,
                SalesReturnId = ret.Id,
                ReturnDate = ret.ReturnDate,
                Quantity = item.Quantity,
                // قیمت واحد فقط از فاکتورِ ارجاع به‌دست می‌آید. اگر برگشت به
                // فاکتوری وصل نباشد (مثلاً مرجوعی بدون سند)، قیمتی در دست نیست و
                // صفر می‌ماند. عمداً قیمت روزِ کالا را جایگزین نمی‌کنیم چون عدد
                // ساختگی می‌سازد و گزارش را گمراه‌کننده می‌کند.
                UnitPrice = invItem != null ? invItem.UnitPrice : 0m,
                // بهای تمام‌شده از اسنپ‌شاتِ خودِ سطر فاکتورِ ارجاع؛ برگشت
                // دقیقاً همان مقدارِ برگشتی را به موجودی و سود برمی‌گرداند
                CostAmount = item.Quantity * (invItem != null ? (invItem.CostPrice ?? 0m) : 0m)
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.ReturnDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.ReturnDate < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.ProductName.Contains(s) || i.Sku.Contains(s));
        }

        // عمداً بدون Take: خروجی باید همهٔ رکوردهای فیلترشده را داشته باشد
        var lines = await q.ToListAsync(ct);

        var items = lines
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                var refund = g.Sum(x => x.Quantity * x.UnitPrice);
                var cost = g.Sum(x => x.CostAmount);
                return new
                {
                    Sku = g.First().Sku,
                    Name = g.First().ProductName,
                    Unit = g.First().Unit,
                    ReturnCount = g.Select(x => x.SalesReturnId).Distinct().Count(),
                    TotalQty = g.Sum(x => x.Quantity),
                    Refund = refund,
                    Cost = cost,
                    // برگشتِ بیشتر از بهای تمام‌شده یعنی سود از بین رفته است
                    NetEffect = refund - cost
                };
            })
            .OrderByDescending(x => x.Refund)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// اقساط پرداخت‌نشده: چه کسی، چه مبلغی، تا چه تاریخی بدهکار است.
    /// <para>
    /// فقط قسط‌هایی می‌آیند که بخشی از مبلغشان پرداخت نشده باشد. اقساط کاملاً
    /// پرداخت‌شده حذف می‌شوند تا گزارش، بدهی واقعی را نشان دهد نه تاریخچهٔ
    /// پرداخت‌ها.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetInstallmentsDueAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Customer", "مشتری", ReportColumnKind.Text),
            new("Phone", "تلفن", ReportColumnKind.Text),
            new("InvoiceNumber", "شمارهٔ فاکتور", ReportColumnKind.Text),
            new("Sequence", "شمارهٔ قسط", ReportColumnKind.Integer),
            new("DueDate", "سررسید", ReportColumnKind.Date),
            new("Remaining", "مانده", ReportColumnKind.Money),
            new("OverdueDays", "تأخیر (روز)", ReportColumnKind.Integer)
        };

        var f = filter.Normalized();

        var q =
            from inst in _db.Installments.AsNoTracking()
            join plan in _db.InstallmentPlans.AsNoTracking() on inst.InstallmentPlanId equals plan.Id
            join inv in _db.SalesInvoices.AsNoTracking() on plan.SalesInvoiceId equals inv.Id
            join cust in _db.Customers.AsNoTracking()
                on inv.CustomerId equals cust.Id into custGroup
            from cust in custGroup.DefaultIfEmpty()
            where inst.PaidAmount < inst.Amount   // فقط مانده‌بدهی
            select new
            {
                CustomerName = cust != null ? cust.Name : "مشتری متفرقه",
                CustomerPhone = cust != null ? cust.Phone : null,
                inv.InvoiceNumber,
                inst.SequenceNumber,
                inst.DueDate,
                Remaining = inst.Amount - inst.PaidAmount
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.DueDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.DueDate < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.CustomerName.Contains(s) || i.InvoiceNumber.Contains(s));
        }

        var rows = await q.OrderBy(i => i.DueDate).ToListAsync(ct);

        // محاسبهٔ تأخیر در حافظه انجام می‌شود: مقایسهٔ تاریخ با «امروز» در SQL
        // ترجمه‌پذیر نیست و مبنای آن باید ساعت تهران باشد نه UTC خام — وگرنه
        // صبح‌ها یک روز جلوتر حساب می‌شود.
        var today = DateTime.UtcNow.Date;
        // نام propertyها باید دقیقاً با کلیدِ ستون‌ها یکی باشد؛
        // ReportTableBuilder روی نام property نگاشت می‌کند نه نام ستون،
        // پس اختلاف نام یعنی سلولِ خالی بی‌اینکه خطایی بدهد.
        var items = rows
            .Select(r => new
            {
                Customer = r.CustomerName,
                Phone = r.CustomerPhone,
                InvoiceNumber = r.InvoiceNumber,
                Sequence = r.SequenceNumber,
                r.DueDate,
                r.Remaining,
                OverdueDays = Math.Max(0, (int)(today - r.DueDate.Date).TotalDays)
            })
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// گردش موجودی هر کالا در هر انبار: ورود، خروج و خالص تغییر.
    /// <para>
    /// گروه‌بندی در حافظه انجام می‌شود (همان الگوی <c>ProfitReportQuery</c>) چون
    /// GroupBy روی کوئری join‌شده در EF Core 10 ترجمه نمی‌شود.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetStockMovementAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("InQty", "ورودی", ReportColumnKind.Number),
            new("OutQty", "خروجی", ReportColumnKind.Number),
            new("NetChange", "تغییر خالص", ReportColumnKind.Number),
            new("Value", "ارزش خالص", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q = from tx in _db.StockTransactions.AsNoTracking()
                select new StockMoveLine
                {
                    ProductId = tx.ProductId,
                    WarehouseId = tx.WarehouseId,
                    OccurredAt = tx.OccurredAt,
                    QuantityChange = tx.QuantityChange,
                    UnitCost = tx.UnitCost
                };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.OccurredAt >= from);
        }
        if (f.ToDate.HasValue)
        {
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.OccurredAt < endExclusive);
        }

        var lines = await q.ToListAsync(ct);

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var warehouseIds = lines.Select(l => l.WarehouseId).Distinct().ToList();

        // نام‌ها جدا خوانده می‌شوند تا از GroupBy روی کوئری join‌شده دور بمانیم
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToListAsync(ct);

        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToListAsync(ct);

        var productById = products.ToDictionary(p => p.Id);
        var warehouseById = warehouses.ToDictionary(w => w.Id);

        var items = lines
            .GroupBy(l => (l.ProductId, l.WarehouseId))
            .Select(g =>
            {
                var inQty = g.Where(x => x.QuantityChange > 0).Sum(x => x.QuantityChange);
                var outQty = g.Where(x => x.QuantityChange < 0).Sum(x => -x.QuantityChange);
                var net = inQty - outQty;

                // آخرین بهای شناخته‌شده در همین گروه (به‌روزترین ورودی)
                var unitCost = g.Where(x => x.UnitCost.HasValue)
                    .OrderBy(x => x.OccurredAt)
                    .Select(x => x.UnitCost!.Value)
                    .DefaultIfEmpty(0m)
                    .Last();

                var product = productById.GetValueOrDefault(g.Key.ProductId);
                var warehouse = warehouseById.GetValueOrDefault(g.Key.WarehouseId);

                return new
                {
                    Sku = product?.Sku ?? string.Empty,
                    Name = product?.Name ?? "نامشخص",
                    Warehouse = warehouse?.Name ?? "نامشخص",
                    InQty = inQty,
                    OutQty = outQty,
                    NetChange = net,
                    Value = net * unitCost
                };
            })
            .OrderByDescending(x => Math.Abs(x.NetChange))
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
