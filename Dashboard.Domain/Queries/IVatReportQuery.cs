namespace Dashboard.Domain.Queries;

/// <summary>
/// گردش یک حساب مالیاتی در یک بازه — سه عددِ «ابتدا/گردش/انتها» از آن ساخته می‌شود.
/// مانده = بدهکار − بستانکار؛ برای ۲۳۰۰ (بدهی) بستانکار یعنی بدهکاریم به سازمان،
/// برای ۱۳۵۰ (اعتبار مالیاتی) بدهکار یعنی اعتبارِ قابل تهاتر.
/// </summary>
public sealed record VatAccountPeriodSummary(
    string AccountCode,
    string AccountName,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit)
{
    /// <summary>مانده پایان دوره به تفکیک طرف — جمعِ ابتدا + گردش</summary>
    public decimal ClosingDebit => OpeningDebit + PeriodDebit;
    public decimal ClosingCredit => OpeningCredit + PeriodCredit;

    /// <summary>مانده بستانکار خالص (برای ۲۳۰۰ مثبت یعنی بدهی مالیاتی)</summary>
    public decimal ClosingCreditBalance => ClosingCredit - ClosingDebit;

    /// <summary>مانده بدهکار خالص (برای ۱۳۵۰ مثبت یعنی اعتبار مالیاتی)</summary>
    public decimal ClosingDebitBalance => ClosingDebit - ClosingCredit;
}

/// <summary>
/// خلاصهٔ اعلامیه ارزش افزوده برای یک بازه — بالای جدولِ گردش نمایش داده می‌شود.
/// <para>
/// «مالیات قابل پرداخت» = بستانکارِ ۲۳۰۰ منهای بدهکارِ ۱۳۵۰: مالیاتِ دریافتی از
/// مشتریان، پس از تهاتر با اعتبار مالیاتیِ پرداختی به تأمین‌کنندگان. عدد منفی یعنی
/// اعتبارِ مالیاتی مانده (بدهکار نیستیم).
/// </para>
/// </summary>
public sealed record VatSummaryDto(
    VatAccountPeriodSummary Payable,    // ۲۳۰۰ — مالیات فروش
    VatAccountPeriodSummary Receivable) // ۱۳۵۰ — اعتبار مالیاتی خرید
{
    public decimal NetVatPayable =>
        Payable.ClosingCreditBalance - Receivable.ClosingDebitBalance;
}

/// <summary>
/// اعلامیه ارزش افزوده — گردش و ماندهٔ حساب‌های ۲۳۰۰ (مالیات فروش) و ۱۳۵۰ (اعتبار خرید).
/// خروجی دو لایه دارد: خلاصهٔ دوره (ابتدا/گردش/پایان هر حساب + خالص قابل پرداخت) و
/// جدولِ خطیِ اسناد. فیلتر تاریخ روی هر دو یکسان اعمال می‌شود تا عدد خلاصه با جدول بخواند؛
/// جست‌وجوی متنی فقط جدول را فیلتر می‌کند (خلاصه‌ی حساب‌ها با جست‌وجو عوض نمی‌شود).
/// </summary>
public interface IVatReportQuery
{
    /// <summary>گردش خطی اسناد روی ۲۳۰۰/۱۳۵۰ — برای جدول صفحه و خروجی اکسل/PDF</summary>
    Task<ReportTable> GetVatLinesAsync(ReportFilter filter, ReportQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>خلاصهٔ دوره: ماندهٔ ابتدا، گردش و خالص قابل پرداخت</summary>
    Task<VatSummaryDto> GetVatSummaryAsync(ReportFilter filter, CancellationToken ct = default);
}
