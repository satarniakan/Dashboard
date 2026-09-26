// Dashboard.Domain/Enums/OrderStatus.cs
namespace Dashboard.Domain.Enums;

public enum OrderStatus
{
    /// <summary>در انتظار پرداخت</summary>
    PendingPayment = 1,
    /// <summary>پرداخت شده</summary>
    Paid = 2,
    /// <summary>در حال پردازش/بسته‌بندی</summary>
    Processing = 3,
    /// <summary>ارسال شده</summary>
    Shipped = 4,
    /// <summary>تحویل داده شده</summary>
    Delivered = 5,
    /// <summary>لغو شده (پرداخت ناموفق، انقضا یا لغو ادمین)</summary>
    Canceled = 6
}
