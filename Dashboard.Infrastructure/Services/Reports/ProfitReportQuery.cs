using Dashboard.Domain.Queries;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>یک گردش موجودی، به شکل تخت و مستقل از navigation.</summary>
internal sealed class StockMoveLine
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public DateTime OccurredAt { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal? UnitCost { get; set; }
}


/// <summary>
/// یک سطر فاکتور، به شکل «تخت» و مستقل از navigation.
/// دلیل وجودش: EF Core نمی‌تواند GroupBy را روی navigation property
/// (مثل i.Product.Name یا i.SalesInvoice.Warehouse.Name) ترجمه کند و
/// InvalidOperationException می‌دهد. پس join را صریح می‌زنیم و در یک
/// رکورد ساده جمع می‌کنیم.
/// </summary>
internal sealed class ProfitLine
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int SalesInvoiceId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>بهای تمام‌شدهٔ همین سطر (اسنپ‌شده، نه قیمت روز کالا)</summary>
    public decimal CostAmount { get; set; }

    public int? WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>تلفن مشتری — در مدل <c>string?</c> است، چون مشتری بدون تلفن هم معتبر است.</summary>
    public string? CustomerPhone { get; set; }
}

/// <summary>
/// گزارش‌های سود — بر پایهٔ CostPrice اسنپ‌شدهٔ SalesInvoiceItem.
/// چون قیمت تمام‌شده در لحظهٔ صدور فاکتور ذخیره شده، سود **دقیق** است و با
/// تغییر قیمت کالای بعدی تغییر نمی‌کند.
/// فقط فاکتورهای «تأییدشده» مبنا هستند: پیش‌نویس فروشی نیست و لغوشده برگشته.
/// </summary>
public class ProfitReportQuery : IProfitReportQuery
{
    private readonly AppDbContext _db;

    public ProfitReportQuery(AppDbContext db) => _db = db;

    /// <summary>
    /// پایهٔ مشترک گزارش‌های سود روی سطح سطر فاکتور.
    /// join کاملاً صریح است و به یک رکورد تخت تبدیل می‌شود، تا GroupBy
    /// بعدی روی دادهٔ اسکالر قابل ترجمه باشد.
    /// دربارهٔ ToDate: تا پایان همان روز انتخابی (exclusive) تا فیلتر «یک روز»
    /// همهٔ فاکتورهای آن روز را بگیرد، نه فقط نیمه‌شب اول.
    /// </summary>
    /// <summary>
    /// کوئری پایهٔ سطرهای فروش، بدون هیچ تجمیعی.
    /// <para>
    /// چرا تجمیع در SQL انجام نمی‌شود: GroupBy روی کوئریِ join‌شده در EF Core 10 با
    /// استثنای «could not be translated» می‌شکند (چون shaper نهایی موجودیت‌های
    /// join‌شده را نگه می‌دارد). راه‌حلِ پایدار: فیلتر و join در SQL (که ترجمه
    /// می‌شود) و تجمیع در حافظه روی ردیف‌های «تخت».
    /// برای این گزارش‌ها بارِ کاری در حد چند هزار تا چند ده‌هزار سطر فروش است،
    /// که برای تجمیع در حافظه بی‌خطر است.
    /// <para>
    /// عمداً هیچ <c>Take</c> یا سقفی اینجا نیست: خروجی اکسل/PDF باید «همهٔ»
    /// رکوردهای فیلترشده را داشته باشد و برش در <c>BuildPaged</c> (فقط برای
    /// مسیر نمایش) انجام می‌شود.
    /// </para>
    /// </summary>
    private IQueryable<ProfitLine> BaseLines(ReportFilter f)
    {
        f = f.Normalized();

        var q =
            from item in _db.SalesInvoiceItems.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on item.SalesInvoiceId equals inv.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            join wh in _db.Warehouses.AsNoTracking() on inv.WarehouseId equals wh.Id into wGroup
            from wh in wGroup.DefaultIfEmpty()
            join cust in _db.Customers.AsNoTracking() on inv.CustomerId equals cust.Id into cGroup
            from cust in cGroup.DefaultIfEmpty()
            where inv.Status == SalesInvoiceStatus.Confirmed
            select new ProfitLine
            {
                ProductId = prod.Id,
                Sku = prod.Sku,
                ProductName = prod.Name,
                Unit = prod.Unit,
                SalesInvoiceId = inv.Id,
                InvoiceDate = inv.InvoiceDate,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal,
                // بهای اسنپ‌شده در لحظهٔ صدور؛ اگر خالی بود، گزارش دروغ نمی‌گوید
                CostAmount = item.Quantity * (item.CostPrice ?? 0m),
                WarehouseId = inv.WarehouseId,
                WarehouseName = wh != null ? wh.Name : string.Empty,
                CustomerId = inv.CustomerId,
                CustomerName = cust != null ? cust.Name : "مشتری متفرقه",
                // Phone در مدل string? است. چون این عبارت داخل کوئری EF ترجمه
                // می‌شود، استفاده از ?? یا string.Empty بی‌فایده است (EF آن‌ها را
                // نادیده می‌گیرد و null برمی‌گرداند) — به همین دلیل پراپرتی
                // CustomerPhone هم nullable است و مصرف‌کننده باید null را تحمل کند.
                CustomerPhone = cust != null ? cust.Phone : string.Empty
            };

        if (f.FromDate.HasValue)
        {
            var from = f.FromDate.Value.Date;
            q = q.Where(i => i.InvoiceDate >= from);
        }
        if (f.ToDate.HasValue)
        {
            // نیمه‌شب اول روزِ بعد، یعنی «کلِ» روز انتخابی داخل بازه است
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.InvoiceDate < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.ProductName.Contains(s) || i.Sku.Contains(s));
        }

        return q;
    }

    public async Task<ReportTable> GetProfitByProductAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("Unit", "واحد", ReportColumnKind.Text),
            new("TotalQty", "تعداد فروش", ReportColumnKind.Number),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Profit", "سود ناخالص", ReportColumnKind.Money),
            new("Margin", "حاشیه سود ٪", ReportColumnKind.Number)
        };

        // تجمیع در حافظه (به دلیل محدودیت ترجمهٔ GroupBy در EF روی کوئری join‌شده).
        // عمداً بدون Take: کاربر خواسته خروجی همهٔ رکوردهای فیلترشده باشد و هیچ
        // سقفی اعمال نشود.
        var lines = await BaseLines(filter).ToListAsync(ct);

        var items = lines
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => x.CostAmount);
                var profit = revenue - cost;
                return new
                {
                    Sku = g.First().Sku,
                    Name = g.First().ProductName,
                    Unit = g.First().Unit,
                    TotalQty = g.Sum(x => x.Quantity),
                    Revenue = revenue,
                    Cost = cost,
                    Profit = profit,
                    Margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100m, 1)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }


    public async Task<ReportTable> GetLossMakingProductsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        // همان محاسبهٔ سود، فقط فیلتر «سود منفی» و مرتب‌سازی نزولیِ زیان
        var columns = new List<ReportColumn>
        {
            new("Sku", "کد کالا", ReportColumnKind.Text),
            new("Name", "نام کالا", ReportColumnKind.Text),
            new("TotalQty", "تعداد فروش", ReportColumnKind.Number),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Loss", "زیان ناخالص", ReportColumnKind.Money)
        };

        // فیلتر «سود منفی» باید روی کل نتیجه اعمال شود و بعد صفحه‌بندی — نه برعکس.
        // اگر اول صفحه‌بندی کنیم و بعد فیلتر کنیم، صفحه‌های بعدی می‌ریزند و مهم‌تر،
        // در مسیر خروجی ردیف‌های سوددهنده هم به اکسل نشت می‌کنند.
        //
        // برای همین کوئریِ کالا همیشه «کامل» صدا زده می‌شود (ForExport) و برش
        // در همین‌جا و بعد از فیلتر زیان انجام می‌گیرد.
        var all = await GetProfitByProductAsync(filter, ReportQueryOptions.ForExport(), ct);

        var lossRows = all.Rows
            .Where(r => r.TryGetValue("Profit", out var p) && p is decimal pd && pd < 0)
            .Select(r => new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase) { ["Loss"] = r["Profit"] })
            .ToList();

        // نکتهٔ مهم: باید به‌صورت صریح به overload دیکشنری اشاره شود. اگر فقط
        // BuildPaged صدا زده شود، C# نسخهٔ عمومیِ <T> را انتخاب می‌کند (چون
        // List<Dictionary<..>> با IReadOnlyList<IReadOnlyDictionary<..>> سازگار است
        // از راه covariance) و آن نسخه روی دیکشنری reflection می‌کند؛ دیکشنری
        // property ندارد، پس همهٔ سلول‌ها null می‌شوند.
        return ReportTableBuilder.BuildPaged(
            columns,
            (IReadOnlyList<IReadOnlyDictionary<string, object?>>)lossRows,
            options);
    }

    public async Task<ReportTable> GetProfitByWarehouseAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Warehouse", "انبار", ReportColumnKind.Text),
            new("InvoiceCount", "تعداد فاکتور", ReportColumnKind.Integer),
            new("TotalQty", "تعداد اقلام", ReportColumnKind.Number),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Profit", "سود ناخالص", ReportColumnKind.Money),
            new("Margin", "حاشیه سود ٪", ReportColumnKind.Number)
        };

        var lines = await BaseLines(filter).ToListAsync(ct);

        var items = lines
            .GroupBy(i => i.WarehouseId ?? 0)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => x.CostAmount);
                var profit = revenue - cost;
                return new
                {
                    Warehouse = g.Select(x => x.WarehouseName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "نامشخص",
                    InvoiceCount = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    TotalQty = g.Sum(x => x.Quantity),
                    Revenue = revenue,
                    Cost = cost,
                    Profit = profit,
                    Margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100m, 1)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    public async Task<ReportTable> GetProfitByCustomerAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Name", "مشتری", ReportColumnKind.Text),
            new("Phone", "تلفن", ReportColumnKind.Text),
            new("InvoiceCount", "تعداد فاکتور", ReportColumnKind.Integer),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Profit", "سود ناخالص", ReportColumnKind.Money),
            new("Margin", "حاشیه سود ٪", ReportColumnKind.Number),
            new("AvgProfitPerInvoice", "میانگین سود هر فاکتور", ReportColumnKind.Money)
        };

        var lines = await BaseLines(filter).ToListAsync(ct);

        var items = lines
            .GroupBy(i => i.CustomerId ?? 0)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => x.CostAmount);
                var profit = revenue - cost;
                var invoiceCount = g.Select(x => x.SalesInvoiceId).Distinct().Count();
                return new
                {
                    Name = g.Select(x => x.CustomerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "مشتری متفرقه",
                    Phone = g.Select(x => x.CustomerPhone).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? string.Empty,
                    InvoiceCount = invoiceCount,
                    Revenue = revenue,
                    Cost = cost,
                    Profit = profit,
                    Margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100m, 1),
                    AvgProfitPerInvoice = invoiceCount == 0 ? 0 : Math.Round(profit / invoiceCount, 0)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }


    /// <summary>
    /// تفکیک ماهانه — روی سطح *فاکتور* (نه سطر) گروه‌بندی می‌شود.
    /// دلیل: تخفیف و حمل‌ونقل در خودِ SalesInvoice هستند؛ اگر روی سطر گروه می‌زدیم،
    /// یک فاکتورِ ۱۰ سطری تخفیفش را ۱۰ برابر جمع می‌کرد.
    /// </summary>
    public async Task<ReportTable> GetProfitByMonthAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Year", "سال", ReportColumnKind.Integer),
            new("Month", "شمارهٔ ماه", ReportColumnKind.Integer),
            new("Period", "ماه (شمسی)", ReportColumnKind.Text),
            new("InvoiceCount", "تعداد فاکتور", ReportColumnKind.Integer),
            new("Revenue", "مبلغ فروش کالا", ReportColumnKind.Money),
            new("Discount", "تخفیف", ReportColumnKind.Money),
            new("Shipping", "حمل‌ونقل", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Profit", "سود ناخالص", ReportColumnKind.Money),
            new("Margin", "حاشیه سود ٪", ReportColumnKind.Number)
        };

        var f = filter.Normalized();
        var q = _db.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Confirmed);

        if (f.FromDate.HasValue) q = q.Where(i => i.InvoiceDate >= f.FromDate.Value);
        if (f.ToDate.HasValue)
        {
            var endExclusive = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.InvoiceDate < endExclusive);
        }
        if (f.Search is not null)
        {
            var s = f.Search;
            q = q.Where(i => i.InvoiceNumber.Contains(s) || i.Customer!.Name.Contains(s));
        }

        var rows = await q
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                InvoiceCount = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount),
                Discount = g.Sum(x => x.DiscountAmount),
                Shipping = g.Sum(x => x.ShippingAmount),
                Cost = g.Sum(x => x.Items.Sum(ii => ii.Quantity * (ii.CostPrice ?? 0m)))
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        // نام ماه اینجا ساخته نمی‌شود: ماه‌های میلادی با ماه‌های شمسی یکی نیستند
        // (ژانویه ≠ فروردین) و PersianDateHelper هم در لایهٔ Application است که
        // Infrastructure به آن دسترسی ندارد. پس «سال + ماه میلادی» برگردانده می‌شود
        // و تبدیل به ماه شمسی در UI (ReportTable) انجام می‌گیرد.
        var items = rows.Select(r =>
        {
            var goodsRevenue = r.Revenue - r.Shipping;
            var profit = goodsRevenue - r.Cost;
            var margin = goodsRevenue == 0 ? 0 : Math.Round(profit / goodsRevenue * 100m, 1);
            return new
            {
                Year = r.Year,
                Month = r.Month,
                Period = new DateTime(r.Year, r.Month, 1),
                r.InvoiceCount,
                Revenue = goodsRevenue,
                r.Discount,
                r.Shipping,
                r.Cost,
                Profit = profit,
                Margin = margin
            };
        }).ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
