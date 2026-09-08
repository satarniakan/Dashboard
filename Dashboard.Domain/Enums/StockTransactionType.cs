namespace Dashboard.Domain.Enums;

/// <summary>
/// نوع رویداد انبار. هر ردیف StockTransaction دقیقاً یکی از این نوع‌ها را دارد.
/// </summary>
public enum StockTransactionType
{
    PurchaseReceipt,      // رسید خرید از تأمین‌کننده (افزایش)
    InternalIssue,        // حواله مصرف داخلی (کاهش)
    SalesReturn,           // برگشت از فروش (افزایش)
    Scrap,                 // ضایعات (کاهش)
    TransferOut,            // خروج به‌خاطر انتقال بین انبار (کاهش)
    TransferIn,              // ورود به‌خاطر انتقال بین انبار (افزایش)
    StockCountIncrease,      // اصلاح مثبت پس از انبارگردانی
    StockCountDecrease,       // اصلاح منفی پس از انبارگردانی
    Sale,               // <- جدید: کاهش موجودی به‌خاطر فروش
    SaleCancellation    // <- جدید: برگشت موجودی به‌خاطر لغو فاکتور
}
