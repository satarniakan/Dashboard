using Dashboard.Domain.Queries;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Services.Reports;

/// <summary>سطر سبد فروشگاه، تخت.</summary>
internal sealed class CartLine
{
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>سطر فروش با اطلاعات دستهٔ کالا، تخت.</summary>
internal sealed class CategorySaleLine
{
    public int ProductId { get; set; }
    public int? CategoryId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal LineTotal { get; set; }
    public decimal CostAmount { get; set; }
    public decimal Quantity { get; set; }
}

/// <summary>
/// گزارش‌های تحلیلی فروشگاه: سبد رهاشده، دستهٔ کالا و جغرافیای فروش.
/// </summary>
public partial class AnalyticsReportQuery : IAnalyticsReportQuery
{
    private readonly AppDbContext _db;

    public AnalyticsReportQuery(AppDbContext db) => _db = db;

    /// <summary>
    /// سبدهای رهاشده: سبدی که منقضی شده ولی هرگز به سفارش تبدیل نشد.
    /// <para>
    /// فقط سبدهای <b>منقضی‌شده</b> می‌آیند: سبدِ فعال هنوز فرصت خرید دارد و
    /// گزارشش زودهنگام است.
    /// </para>
    /// <para>
    /// «تبدیل‌نشده» از این واقعیت تشخیص داده می‌شود که سبد به کاربرِ
    /// <b>وارد‌شده</b> وصل نشده — سبد مهمان با <c>CookieId</c> شناسایی می‌شود و
    /// وقتی لاگین کند و تسویه کند، سبد به سفارش تبدیل و حذف می‌شود.
    /// </para>
    /// <para>
    /// ارزش سبد از <b>قیمت فعلی کالا</b> محاسبه می‌شود چون <c>CartItem</c> قیمت
    /// ذخیره نمی‌کند. این یعنی ارزشِ امروزِ آن سبد است، نه قیمتی که مشتری آن
    /// روز دید — برای برآورد درآمد از‌دست‌رفته همین عدد درست است.
    /// </para>
    /// </summary>
    public async Task<ReportTable> GetAbandonedCartsAsync(
        ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default)
    {
        var columns = new List<ReportColumn>
        {
            new("CartId", "شناسهٔ سبد", ReportColumnKind.Integer),
            new("CreatedAt", "زمان ساخت", ReportColumnKind.Date),
            new("ExpiredAt", "زمان انقضا", ReportColumnKind.Date),
            new("AgeHours", "سن (ساعت)", ReportColumnKind.Integer),
            new("ItemCount", "تعداد کالا", ReportColumnKind.Integer),
            new("TotalQty", "مقدار کل", ReportColumnKind.Number),
            new("CartValue", "ارزش تقریبی سبد", ReportColumnKind.Money),
            new("TopProduct", "بالاترین ارزش سبد", ReportColumnKind.Text)
        };

        var f = filter.Normalized();
        var now = DateTime.UtcNow;

        // فقط سبدهای منقضی‌شده. سبدِ مهمانی که به سفارش تبدیل شده، هنگام
        // تسویه حذف می‌شود، پس باقی‌ماندنِ سبد یعنی رها شده است. برای اطمینان
        // بیشتر، سبدهای خالی هم کنار گذاشته می‌شوند: سبد بی‌کالا درآمدی ندارد.
        var q =
            from c in _db.Carts.AsNoTracking()
            where c.ExpiresAt < now
            select c;

        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(c => c.CreatedAt >= from); }
        if (f.ToDate.HasValue)
        {
            var end = f.ToDate.Value.Date.AddDays(1);
            q = q.Where(c => c.CreatedAt < end);
        }

        // ⚠️ عمداً هیچ Take/Tkip اینجا نیست: مسیرِ خروجی اکسل باید «همهٔ
        // سبدهای فیلترشده» را بدهد و برش فقط در BuildPaged (مسیر نمایش) انجام
        // می‌شود — همان قاعدهٔ بقیهٔ گزارش‌ها. سقفِ ۱۰٬۰۰۰ ردیف قبلاً
        // سطرها را بی‌صدا حذف می‌کرد، یعنی اکسل ناقص بود بدون هیچ هشداری.
        var carts = await q
            .OrderByDescending(c => c.ExpiresAt)
            .ToListAsync(ct);

        if (carts.Count == 0)
            return ReportTableBuilder.BuildPaged(columns, new List<object>(), options);

        var cartIds = carts.Select(c => c.Id).ToList();

        // سطرهای سبد جدا خوانده می‌شوند: GroupBy روی کوئری join‌شده در EF
        // Core 10 ترجمه نمی‌شود (همان الگوی بقیهٔ گزارش‌ها).
        // CartItem قیمت ندارد، پس ارزش از قیمت فعلیِ کالا خوانده می‌شود.
        var itemRows = await _db.CartItems.AsNoTracking()
            .Where(i => cartIds.Contains(i.CartId))
            .Select(i => new { i.CartId, i.ProductId, i.Quantity })
            .ToListAsync(ct);

        var productIds = itemRows.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Price })
            .ToListAsync(ct);
        var productById = products.ToDictionary(p => p.Id);

        // سبدهای خالی ارزشی ندارند و فقط نویزند، پس کنار گذاشته می‌شوند.
        // اگر سبدی سطر نداشته باشد، اصلاً در فهرست سبدهایِ دارای محتوا نیست.
        var cartIdsWithItems = itemRows.Select(i => i.CartId).Distinct().ToHashSet();

        var items = carts
            .Where(c => cartIdsWithItems.Contains(c.Id))
            .Select(c =>
            {
                var mine = itemRows.Where(i => i.CartId == c.Id).ToList();

                // ارزش هر سطر = مقدار × قیمت فعلی کالا (CartItem قیمت ندارد)
                decimal ValueOf(int productId) =>
                    productById.TryGetValue(productId, out var p) ? p.Price : 0m;

                // بالاترین ارزش سبد: کالایی که بیشترین پول را در سبد نگه داشته
                var top = mine
                    .OrderByDescending(i => i.Quantity * ValueOf(i.ProductId))
                    .Select(i => productById.GetValueOrDefault(i.ProductId)?.Name)
                    .FirstOrDefault();

                return new
                {
                    CartId = c.Id,
                    c.CreatedAt,
                    ExpiredAt = c.ExpiresAt,
                    AgeHours = (int)Math.Max(0, (now - c.CreatedAt).TotalHours),
                    ItemCount = mine.Count,
                    TotalQty = mine.Sum(i => i.Quantity),
                    CartValue = mine.Sum(i => i.Quantity * ValueOf(i.ProductId)),
                    TopProduct = top ?? "—"
                };
            })
            .OrderByDescending(x => x.CartValue)
            .ToList();

        return ReportTableBuilder.BuildPaged(columns, items, options);
    }
}
