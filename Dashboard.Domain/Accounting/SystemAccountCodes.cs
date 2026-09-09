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

    /// <summary>درآمد فروش کالا</summary>
    public const string SalesRevenue = "4000";

    /// <summary>بهای تمام‌شده‌ی کالای فروش‌رفته (COGS)</summary>
    public const string CostOfGoodsSold = "5000";
}
