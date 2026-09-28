using Dashboard.Domain.Enums;
using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>گزارش‌های فروشگاه اینترنتی و ممیزی.</summary>
public partial class FinancialReportQuery
{
    /// <summary>
    /// درآمد فروشگاه اینترنتی: سفارش‌های پرداخت‌شده و ارزش آن‌ها.
    /// <para>
    /// فقط سفارش‌های پرداخت‌شده حساب می‌شوند. سفارش «در انتظار پرداخت» هنوز
    /// درآمدی نیست و «لغوشده» هم هرگز نبوده — اگر همه را جمع بزنیم، درآمد
    /// واقعی بیشتر از واقع نشان داده می‌شود.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetStoreRevenueAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("OrderNumber", "شمارهٔ سفارش", ReportColumnKind.Text),
            new("CreatedAt", "تاریخ ثبت", ReportColumnKind.Date),
            new("PaidAt", "تاریخ پرداخت", ReportColumnKind.Date),
            new("Customer", "مشتری", ReportColumnKind.Text),
            new("City", "شهر", ReportColumnKind.Text),
            new("Subtotal", "جمع کالا", ReportColumnKind.Money),
            new("Discount", "تخفیف", ReportColumnKind.Money),
            new("Shipping", "حمل‌ونقل", ReportColumnKind.Money),
            new("Total", "مبلغ نهایی", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        // وضعیت‌هایی که پرداخت شده‌اند: Paid، Processing، Shipped، Delivered
        var paid = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };

        var q =
            from o in _db.Orders.AsNoTracking()
            where paid.Contains(o.Status)
            select new
            {
                o.OrderNumber, o.CreatedAt, o.PaidAt, o.CustomerName, o.City,
                o.Subtotal, o.DiscountAmount, o.ShippingCost
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
            q = q.Where(i => i.OrderNumber.Contains(s) || i.CustomerName.Contains(s) || i.City.Contains(s));
        }

        var rows = await q.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.OrderNumber, r.CreatedAt, r.PaidAt,
            Customer = r.CustomerName,
            City = r.City,
            r.Subtotal,
            Discount = r.DiscountAmount,
            Shipping = r.ShippingCost,
            Total = r.Subtotal - r.DiscountAmount + r.ShippingCost
        }).ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>
    /// عملکرد سفارش‌ها و نرخ تبدیل.
    /// <para>
    /// نرخ تبدیل = پرداخت‌شده ÷ کل سفارش‌ها. سفارش لغوشده جزو «کل» است چون
    /// مشتری آن را نهایی نکرده — وگرنه نرخ تبدیل همیشه خوش‌بینانه می‌شود.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetOrderConversionAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Status", "وضعیت", ReportColumnKind.Text),
            new("OrderCount", "تعداد سفارش", ReportColumnKind.Integer),
            new("Share", "سهم", ReportColumnKind.Number),
            new("AvgTotal", "میانگین مبلغ", ReportColumnKind.Money),
            new("TotalValue", "ارزش کل", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        var q = _db.Orders.AsNoTracking()
            .Select(o => new { o.Status, o.Subtotal, o.DiscountAmount, o.ShippingCost, o.CreatedAt });

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.CreatedAt >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.CreatedAt < end);
        }

        var rows = await q.ToListAsync(ct);
        var total = rows.Count;

        var items = rows
            .GroupBy(r => r.Status)
            .Select(g =>
            {
                var value = g.Sum(r => r.Subtotal - r.DiscountAmount + r.ShippingCost);
                return new
                {
                    Status = StatusLabel(g.Key),
                    OrderCount = g.Count(),
                    Share = total == 0 ? 0 : Math.Round(g.Count() * 100m / total, 1),
                    AvgTotal = g.Count() == 0 ? 0 : Math.Round(value / g.Count()),
                    TotalValue = value
                };
            })
            .OrderByDescending(x => x.OrderCount)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>برچسب فارسی وضعیت سفارش.</summary>
    private static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت",
        OrderStatus.Paid => "پرداخت‌شده",
        OrderStatus.Processing => "در حال پردازش",
        OrderStatus.Shipped => "ارسال‌شده",
        OrderStatus.Delivered => "تحویل‌شده",
        OrderStatus.Canceled => "لغوشده",
        _ => status.ToString()
    };

    /// <summary>
    /// اثربخشی کدهای تخفیف در فروشگاه.
    /// <para>
    /// ارزش واقعی کد = مبلغ فروشِ سفارش‌هایی که از آن استفاده کرده‌اند. اگر این
    /// عدد از هزینهٔ تخفیف بیشتر نباشد، کد عملاً زیان‌ده بوده — همین مقایسه
    /// تصمیمِ نگه‌داشتن یا حذف کد را ممکن می‌کند.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetDiscountPerformanceAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("Code", "کد تخفیف", ReportColumnKind.Text),
            new("Type", "نوع", ReportColumnKind.Text),
            new("Value", "مقدار", ReportColumnKind.Number),
            new("UsedOrders", "سفارش استفاده‌کننده", ReportColumnKind.Integer),
            new("TotalDiscount", "تخفیف داده‌شده", ReportColumnKind.Money),
            new("GeneratedRevenue", "فروش ایجادشده", ReportColumnKind.Money),
            new("AvgOrder", "میانگین سبد", ReportColumnKind.Money)
        };

        var f = filter.Normalized();

        // فقط سفارش‌های پرداخت‌شده: سفارش لغوشده فروشی ایجاد نکرده
        var paid = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };

        var q =
            from o in _db.Orders.AsNoTracking()
            where o.DiscountCodeText != null && o.DiscountCodeText != "" && paid.Contains(o.Status)
            select new
            {
                Code = o.DiscountCodeText,
                o.DiscountAmount,
                Total = o.Subtotal - o.DiscountAmount + o.ShippingCost,
                o.CreatedAt
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
            q = q.Where(i => i.Code.Contains(s));
        }

        var rows = await q.ToListAsync(ct);

        // تعریف کدها جدا خوانده می‌شود تا نوع و درصدشان کنار آمار بیاید
        var codes = rows.Select(r => r.Code).Distinct().ToList();
        var definitions = await _db.DiscountCodes.AsNoTracking()
            .Where(d => codes.Contains(d.Code))
            .ToListAsync(ct);
        var byCode = definitions.ToDictionary(d => d.Code);

        var items = rows
            .GroupBy(r => r.Code)
            .Select(g =>
            {
                var discount = g.Sum(x => x.DiscountAmount);
                var revenue = g.Sum(x => x.Total);
                byCode.TryGetValue(g.Key, out var def);
                return new
                {
                    Code = g.Key,
                    Type = def != null
                        ? (def.Type == DiscountType.Percentage ? "درصدی" : "مبلغ ثابت")
                        : "—",
                    Value = def?.Value ?? 0m,
                    UsedOrders = g.Count(),
                    TotalDiscount = discount,
                    GeneratedRevenue = revenue,
                    AvgOrder = g.Count() == 0 ? 0 : Math.Round(revenue / g.Count())
                };
            })
            .OrderByDescending(x => x.GeneratedRevenue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }

    /// <summary>گزارش ممیزی: چه کسی چه چیزی را چه زمانی تغییر داد.</summary>
    public async Task<ReportTable> GetAuditLogAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("OccurredAt", "زمان", ReportColumnKind.Date),
            new("EventType", "رویداد", ReportColumnKind.Text),
            new("UserEmail", "کاربر", ReportColumnKind.Text),
            new("Details", "جزئیات", ReportColumnKind.Text)
        };

        var f = filter.Normalized();

        var q = _db.AuditLogs.AsNoTracking()
            .Select(a => new { a.OccurredAt, a.EventType, a.UserEmail, a.Details });

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(i => i.OccurredAt >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(i => i.OccurredAt < end);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(i => i.EventType.Contains(s) || i.Details.Contains(s)
                             || (i.UserEmail != null && i.UserEmail.Contains(s)));
        }

        var rows = await q.OrderByDescending(i => i.OccurredAt).ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.OccurredAt, r.EventType,
            UserEmail = r.UserEmail ?? "نامشخص",
            r.Details
        }).ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
