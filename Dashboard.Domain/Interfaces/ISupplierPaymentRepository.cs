using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Interfaces;

public interface ISupplierPaymentRepository
{
    Task<IEnumerable<SupplierPayment>> GetAllAsync();
    Task<SupplierPayment?> GetByIdAsync(int id);
    Task<List<SupplierPayment>> GetPendingChequesAsync();
    Task AddAsync(SupplierPayment payment);
    Task UpdateAsync(SupplierPayment payment);

    /// <summary>
    /// ثبت وضعیت چک به‌صورت اتمیک: فقط اگر هنوز Pending (یا ست‌نشده) باشد تغییر می‌کند.
    /// جلوی دوبرابرشدن سند وصول/برگشت با دوبار کلیک یا دو کاربر همزمان را می‌گیرد.
    /// </summary>
    Task<bool> TryClaimChequeStatusAsync(int paymentId, ChequeStatus status);
}