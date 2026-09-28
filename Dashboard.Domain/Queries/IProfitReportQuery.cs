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

/// <summary>
/// گزارش‌های تأمین، کنترل و جابه‌جایی کالا (بخش دوم فاز ۲).
/// این‌ها تاریخچه دارند، پس برخلاف <see cref="IStockReportQuery"/> فیلتر بازهٔ
/// تاریخ رویشان معنادار است.
/// </summary>
public interface IPurchaseReportQuery
{
    /// <summary>رسیدهای خرید به تفکیک تأمین‌کننده — برای بررسی قیمت و حجم خرید.</summary>
    Task<ReportTable> GetPurchaseBySupplierAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>بهای تمام‌شدهٔ واقعی (میانگین موزون) در بازه — برای راستی‌آزمایی محاسبهٔ سود.</summary>
    Task<ReportTable> GetActualCostByProductAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>ضایعات و حوالهٔ مصرف داخلی با هم — هر دو یعنی «خروج بدون فروش».</summary>
    Task<ReportTable> GetStockLossAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>انتقال بین انبارها: کالا از کجا به کجا رفت.</summary>
    Task<ReportTable> GetWarehouseTransfersAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>انبارگردانی: موجودی سیستمی در برابر شمارش واقعی و اختلاف.</summary>
    Task<ReportTable> GetStockCountVarianceAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}
/// <summary>
/// گزارش‌های مالی و فروشگاه (فاز ۳).
/// همهٔ این‌ها تاریخچه دارند، پس فیلتر بازهٔ تاریخ روی همه معنادار است.
/// </summary>
public interface IFinancialReportQuery
{
    /// <summary>دفتر کل: ماندهٔ بدهکار/بستانکار هر حساب در بازه.</summary>
    Task<ReportTable> GetTrialBalanceAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>گردش هر حساب صندوق/بانک — برای تطبیق با گردش واقعی.</summary>
    Task<ReportTable> GetAccountTransactionsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>دریافت‌ها از مشتریان، به تفکیک روش پرداخت.</summary>
    Task<ReportTable> GetCustomerReceiptsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>پرداخت‌ها به تأمین‌کنندگان، به تفکیک روش پرداخت.</summary>
    Task<ReportTable> GetSupplierPaymentsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>درآمد فروشگاه اینترنتی: سفارش‌ها و ارزش آن‌ها.</summary>
    Task<ReportTable> GetStoreRevenueAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>عملکرد سفارش‌ها و نرخ تبدیل (پرداخت‌شده ÷ کل).</summary>
    Task<ReportTable> GetOrderConversionAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>اثربخشی کدهای تخفیف: چند بار استفاده شد و چقدر فروش آورد.</summary>
    Task<ReportTable> GetDiscountPerformanceAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>گزارش ممیزی: چه کسی چه چیزی را چه زمانی تغییر داد.</summary>
    Task<ReportTable> GetAuditLogAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// گزارش‌های تحلیلی و تفصیلی (فاز ۴).
/// این گزارش‌ها به داده‌هایی نگاه می‌کنند که گزارش‌های قبلی پوشش نداده بودند:
/// سبد رهاشده، دستهٔ کالا، جغرافیای فروش و حساب تفصیلی طرف حساب.
/// </summary>
public interface IAnalyticsReportQuery
{
    /// <summary>سبدهای رهاشده فروشگاه — درآمدی که از دست رفته.</summary>
    Task<ReportTable> GetAbandonedCartsAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>فروش به تفکیک دستهٔ کالا — برای تصمیم خرید و چیدمان فروشگاه.</summary>
    Task<ReportTable> GetSalesByCategoryAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>فروش به تفکیک استان و شهر — برای برنامه‌ریزی ارسال و بازاریابی.</summary>
    Task<ReportTable> GetSalesByRegionAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>حساب تفصیلی: بدهکاری/بستانکاری هر مشتری یا تأمین‌کننده.</summary>
    Task<ReportTable> GetSubsidiaryLedgerAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);
}
