namespace Dashboard.Domain.Enums;

/// <summary>
/// چرخهٔ عمر چک. فقط برای Method == Cheque معنا دارد (برای بقیه null می‌ماند).
/// ثبت چک = Pending (در اوراق ۱۱۴۰/۲۲۰۰ می‌نشیند)؛ وصول = پول واقعاً به بانک رسید؛
/// برگشتی = چک پاس نشد و بدهی/طلب به حالت قبل برمی‌گردد.
/// </summary>
public enum ChequeStatus
{
    Pending,   // در جریان — هنوز سررسید نشده یا وصول نشده
    Settled,   // وصول‌شده — پول نقد شد
    Bounced    // برگشت‌خورده — پاس نشد
}
