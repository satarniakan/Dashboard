namespace Dashboard.Domain.Queries;

/// <summary>
/// گزارش‌های سود و زیان.
/// قرارداد در Domain است تا UI بتواند آن را تزریق کند، ولی پیاده‌سازیِ
/// EF آن در Infrastructure می‌ماند (قانون: وابستگی فقط رو به پایین).
/// </summary>
public interface IProfitReportQuery
{
    /// <param name="filter">فیلتر تاریخ/جست‌وجو — در هر دو مسیر یکسان است تا عدد
    /// خروجی اکسل دقیقاً همان چیزی باشد که کاربر روی صفحه می‌بیند.</param>
    /// <param name="options">
    /// برای نمایش، <see cref="ReportQueryOptions.ForPage"/> و برای اکسل،
    /// <see cref="ReportQueryOptions.ForExport"/> (بدون سقف). پیش‌فرض = نمایشِ صفحهٔ اول.
    /// </param>
    Task<ReportTable> GetProfitByProductAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
    Task<ReportTable> GetProfitByWarehouseAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
    Task<ReportTable> GetProfitByCustomerAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
    Task<ReportTable> GetProfitByMonthAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
    Task<ReportTable> GetLossMakingProductsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// گزارش‌های فروش/انبار مکملِ گزارش سود.
/// قرارداد در Domain و پیاده‌سازیِ EF در Infrastructure (قانون: وابستگی فقط رو به پایین).
/// پارامترها مثل <see cref="IProfitReportQuery"/> است: نمایش صفحه‌بندی‌شده،
/// خروجی بدون سقف.
/// </summary>
public interface ISalesOperationsReportQuery
{
    /// <summary>برگشت از فروش به تفکیک کالا — اثر مستقیم بر سود را نشان می‌دهد.</summary>
    Task<ReportTable> GetSalesReturnsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>مطالبات اقساطی: چه کسی، چه مبلغی، تا چه تاریخی بدهکار است.</summary>
    Task<ReportTable> GetInstallmentsDueAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>گردش موجودی هر کالا در انبار: ورودی، خروجی و خالص تغییر.</summary>
    Task<ReportTable> GetStockMovementAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// گزارش‌های وضعیت موجودی (فاز ۲ — انبار).
/// توجه: این گزارش‌ها «لحظهٔ حال» را نشان می‌دهند، نه یک بازهٔ تاریخی،
/// پس فیلتر تاریخ روی آن‌ها معنی ندارد و فقط فیلتر جست‌وجو کاربرد دارد.
/// </summary>
public interface IStockReportQuery
{
    /// <summary>موجودی فعلی به تفکیک کالا و انبار، همراه ارزش و نقطهٔ سفارش.</summary>
    Task<ReportTable> GetStockOnHandAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>فقط کالاهایی که موجودی‌شان به نقطهٔ سفارش رسیده یا زیر آن است.</summary>
    Task<ReportTable> GetBelowReorderPointAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}
