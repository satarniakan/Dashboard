using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Interfaces;

public interface ICustomerReceiptRepository
{
    Task<IEnumerable<CustomerReceipt>> GetAllAsync();
    Task<CustomerReceipt?> GetByIdAsync(int id);
    Task<List<CustomerReceipt>> GetPendingChequesAsync();
    Task AddAsync(CustomerReceipt receipt);
    Task UpdateAsync(CustomerReceipt receipt);

    /// <summary>
    /// ثبت وضعیت چک به‌صورت اتمیک: فقط اگر هنوز Pending (یا ست‌نشده) باشد تغییر می‌کند.
    /// جلوی دوبرابرشدن سند وصول/برگشت با دوبار کلیک یا دو کاربر همزمان را می‌گیرد.
    /// </summary>
    Task<bool> TryClaimChequeStatusAsync(int receiptId, ChequeStatus status);
}