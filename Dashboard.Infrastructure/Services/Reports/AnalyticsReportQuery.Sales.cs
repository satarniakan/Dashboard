using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>فروش به تفکیک دستهٔ کالا و جغرافیا.</summary>
public partial class AnalyticsReportQuery
{
    /// <summary>
    /// فروش به تفکیک دستهٔ کالا.
    /// <para>
    /// کالای بدون دسته، دستهٔ «بدون دسته‌بندی» می‌گیرد — چون اگر حذف شود، ارزش
    /// فروش آن کالا از گزارش ناپدید می‌شود و مجموع با گزارش سود نمی‌خواند.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetSalesByCategoryAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Category", "دستهٔ کالا", ReportColumnKind.Text),
            new("ProductCount", "تعداد کالا", ReportColumnKind.Integer),
            new("TotalQty", "مقدار فروش", ReportColumnKind.Number),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("Cost", "بهای تمام‌شده", ReportColumnKind.Money),
            new("Profit", "سود ناخالص", ReportColumnKind.Money),
            new("Margin", "حاشیه سود ٪", ReportColumnKind.Number),
            new("Share", "سهم از فروش ٪", ReportColumnKind.Number)
        };

        var lines = await BaseSaleLines(filter, ct);

        var categoryIds = lines.Select(l => l.CategoryId).Where(id => id.HasValue).Distinct().ToList();
        var categories = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);
        var nameById = categories.ToDictionary(c => c.Id, c => c.Name);

        var grouped = lines
            .GroupBy(l => l.CategoryId ?? 0)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => x.CostAmount);
                return new
                {
                    Category = g.Key == 0 ? "بدون دسته‌بندی" : nameById.GetValueOrDefault(g.Key) ?? "نامشخص",
                    ProductCount = g.Select(x => x.ProductId).Distinct().Count(),
                    TotalQty = g.Sum(x => x.Quantity),
                    Revenue = revenue,
                    Cost = cost,
                    Profit = revenue - cost
                };
            })
            .ToList();

        // سهم هر دسته از کل فروش — برای مقایسهٔ سریع بین دسته‌ها
        var totalRevenue = grouped.Sum(x => x.Revenue);

        var items = grouped
            .Select(g => new
            {
                g.Category, g.ProductCount, g.TotalQty, g.Revenue, g.Cost, g.Profit,
                Margin = g.Revenue == 0 ? 0 : Math.Round(g.Profit / g.Revenue * 100m, 1),
                Share = totalRevenue == 0 ? 0 : Math.Round(g.Revenue / totalRevenue * 100m, 1)
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>فروش به تفکیک استان و شهر — برای برنامه‌ریزی ارسال.</summary>
    public async Task<ReportTable> GetSalesByRegionAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Province", "استان", ReportColumnKind.Text),
            new("City", "شهر", ReportColumnKind.Text),
            new("OrderCount", "تعداد سفارش", ReportColumnKind.Integer),
            new("TotalQty", "تعداد کالا", ReportColumnKind.Integer),
            new("Revenue", "مبلغ فروش", ReportColumnKind.Money),
            new("AvgOrder", "میانگین سبد", ReportColumnKind.Money),
            new("Share", "سهم ٪", ReportColumnKind.Number)
        };

        var f = filter.Normalized();
        var paid = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };

        var q =
            from o in _db.Orders.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on o.SalesInvoiceId equals inv.Id
                into invGroup
            from inv in invGroup.DefaultIfEmpty()
            where paid.Contains(o.Status)
            select new
            {
                o.Province, o.City, o.CreatedAt,
                Total = o.Subtotal - o.DiscountAmount + o.ShippingCost,
                ItemCount = o.Items.Count
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.CreatedAt >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.Province.Contains(s) || i.City.Contains(s));
        }

        var rows = await q.ToListAsync(ct);
        var total = rows.Sum(r => r.Total);

        var items = rows
            .GroupBy(r => new { r.Province, r.City })
            .Select(g =>
            {
                var revenue = g.Sum(x => x.Total);
                return new
                {
                    Province = string.IsNullOrWhiteSpace(g.Key.Province) ? "نامشخص" : g.Key.Province,
                    City = string.IsNullOrWhiteSpace(g.Key.City) ? "نامشخص" : g.Key.City,
                    OrderCount = g.Count(),
                    TotalQty = g.Sum(x => x.ItemCount),
                    Revenue = revenue,
                    AvgOrder = g.Count() == 0 ? 0 : Math.Round(revenue / g.Count()),
                    Share = total == 0 ? 0 : Math.Round(revenue / total * 100m, 1)
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// حساب تفصیلی طرف حساب: هر مشتری یا تأمین‌کننده چه‌قدر بدهکار/بستانکار است.
    /// <para>
    /// برخلاف گزارش اقساط که فقط «مانده» را نشان می‌دهد، اینجا ریزِ گردش هم
    /// می‌آید تا معلوم شود بدهی از کجا و چه زمانی ایجاد شده.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetSubsidiaryLedgerAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Type", "نوع", ReportColumnKind.Text),
            new("Name", "طرف حساب", ReportColumnKind.Text),
            new("EntryCount", "تعداد سند", ReportColumnKind.Integer),
            new("TotalDebit", "جمع بدهکار", ReportColumnKind.Money),
            new("TotalCredit", "جمع بستانکار", ReportColumnKind.Money),
            new("Balance", "مانده", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q =
            from line in _db.JournalEntryLines.AsNoTracking()
            join entry in _db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where line.SubsidiaryId != null && line.SubsidiaryType != null
            select new
            {
                Type = line.SubsidiaryType,
                SubsidiaryId = line.SubsidiaryId!.Value,
                entry.EntryDate,
                Debit = line.DebitAmount,
                Credit = line.CreditAmount
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(l => l.EntryDate >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(l => l.EntryDate < end);
        }

        var rows = await q.ToListAsync(ct);
        if (rows.Count == 0)
            return ReportTableBuilder.BuildPaged(columns, new List<object>(), options);

        // نام طرف حساب جدا خوانده می‌شود چون نوعش مشخص نیست
        var customerIds = rows.Where(r => r.Type == "Customer").Select(r => r.SubsidiaryId).Distinct().ToList();
        var supplierIds = rows.Where(r => r.Type == "Supplier").Select(r => r.SubsidiaryId).Distinct().ToList();

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);
        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        var nameByKey = new Dictionary<(string, int), string>();
        foreach (var c in customers) nameByKey[("Customer", c.Id)] = c.Name;
        foreach (var s in suppliers) nameByKey[("Supplier", s.Id)] = s.Name;

        var items = rows
            .GroupBy(r => (r.Type!, r.SubsidiaryId))
            .Select(g =>
            {
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                return new
                {
                    Type = g.Key.Item1 == "Customer" ? "مشتری" : "تأمین‌کننده",
                    Name = nameByKey.GetValueOrDefault(g.Key, "نامشخص"),
                    EntryCount = g.Count(),
                    TotalDebit = debit,
                    TotalCredit = credit,
                    Balance = debit - credit
                };
            })
            // بیشترین بدهی اول — عملیاتی‌ترین ترتیب برای وصول و پرداخت
            .OrderByDescending(x => x.Balance)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>سطرهای پایهٔ فروش تأییدشده، تخت و مستقل از navigation.</summary>
    private async Task<List<CategorySaleLine>> BaseSaleLines(ReportFilter filter, CancellationToken ct)
    {
        var f = filter.Normalized();

        var q =
            from item in _db.SalesInvoiceItems.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on item.SalesInvoiceId equals inv.Id
            join prod in _db.Products.AsNoTracking() on item.ProductId equals prod.Id
            where inv.Status == SalesInvoiceStatus.Confirmed
            select new CategorySaleLine
            {
                ProductId = prod.Id,
                CategoryId = prod.CategoryId,
                InvoiceDate = inv.InvoiceDate,
                Sku = prod.Sku,
                ProductName = prod.Name,
                LineTotal = item.LineTotal,
                // بهای اسنپ‌شده در لحظهٔ صدور، نه قیمت روز کالا
                CostAmount = item.Quantity * (item.CostPrice ?? 0m),
                Quantity = item.Quantity
            };

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.InvoiceDate >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.InvoiceDate < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.ProductName.Contains(s) || i.Sku.Contains(s));
        }

        return await q.ToListAsync(ct);
    }
}
