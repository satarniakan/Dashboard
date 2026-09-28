namespace Dashboard.Domain.Accounting;

/// <summary>
/// کدهای سرفصل‌های سیستمی حسابداری — دقیقاً همان کدهایی که <c>ChartOfAccountsSeeder</c> در دیتابیس می‌سازد.
/// دلیل وجود این کلاس: قبلاً این کدها به‌صورت رشته‌ی ثابت ("1200"، "4000" و ...) هم در
/// <c>SalesService</c> و هم در <c>TreasuryService</c> تکرار شده بودند. اگر روزی بخواهیم
/// یک کد را تغییر بدهیم، باید همه‌ی جاهای پخش‌شده را پیدا و اصلاح کنیم — کاری که خیلی راحت
/// از قلم می‌افتد و باعث خطای «حساب یافت نشد» در زمان اجرا می‌شود، نه در زمان کامپایل.
/// از این به بعد فقط از همین کلاس استفاده کنید.
/// </summary>
public static class SystemAccountCodes
{
    /// <summary>صندوق — والد سرفصل صندوق‌های نقدی (خود صندوق‌های واقعی زیرمجموعه‌ی این کد نیستند، هرکدام سرفصل جدا دارند)</summary>
    public const string Cash = "1100";

    /// <summary>حساب‌های دریافتنی تجاری (طلب از مشتریان)</summary>
    public const string AccountsReceivable = "1200";

    /// <summary>موجودی کالا</summary>
    public const string Inventory = "1300";

    /// <summary>حساب‌های پرداختنی تجاری (بدهی به تأمین‌کنندگان)</summary>
    public const string AccountsPayable = "2100";

    /// <summary>مالیات و عوارض بر ارزش افزودهٔ فروش — بدهی. هنگام فروش بستانکار می‌شود
    /// (مشتری آن را می‌پردازد) و با پرداخت دوره‌ای به سازمان امور مالیاتی تسویه می‌شود.</summary>
    public const string VatPayable = "2300";

    /// <summary>مالیات و عوارض بر ارزش افزودهٔ خرید — اعتبار مالیاتی (دارایی). هنگام خرید
    /// بدهکار می‌شود و مانده‌اش با ۲۳۰۰ تهاتر می‌شود؛ بهای تمام‌شدهٔ کالا خالص می‌ماند.</summary>
    public const string VatReceivable = "1350";

    /// <summary>
    /// اوراق دریافتنی (چک/سفتهٔ دریافتی از مشتری) — دارایی. وصول چک نقد شدن این سند است،
    /// نه ثبت آن روی روزِ دریافت؛ پس چک روزِ دریافت به صندوق/بانک نمی‌خورد.
    /// </summary>
    public const string NotesReceivable = "1140";

    /// <summary>اوراق پرداختنی (چک/سفتهٔ صادره به تأمین‌کننده) — بدهی.</summary>
    public const string NotesPayable = "2200";

    /// <summary>درآمد فروش کالا</summary>
    public const string SalesRevenue = "4000";

    /// <summary>درآمد خدمات/حمل‌ونقل — هزینهٔ ارسال سفارش که مشتری جدا از کالا پرداخت می‌کند</summary>
    public const string ShippingRevenue = "4100";

    /// <summary>بهای تمام‌شده‌ی کالای فروش‌رفته (COGS)</summary>
    public const string CostOfGoodsSold = "5000";

    /// <summary>هزینهٔ کالای مصرف‌شده با حوالهٔ داخلی (غیرفروش)</summary>
    public const string InternalIssueExpense = "6100";

    /// <summary>هزینهٔ ضایعات انبار</summary>
    public const string ScrapExpense = "6200";

    /// <summary>کسری شمارش انبارگردانی (اختلاف منفی)</summary>
    public const string InventoryShortage = "6300";

    /// <summary>اضافات کشف‌شده در انبارگردانی (اختلاف مثبت) — سایر درآمدها</summary>
    public const string InventorySurplus = "4900";
}
