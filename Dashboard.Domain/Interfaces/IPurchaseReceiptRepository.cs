using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IPurchaseReceiptRepository
{
    Task<PurchaseReceipt?> GetByIdAsync(int id);
    Task<PurchaseReceipt?> GetByReceiptNumberAsync(string receiptNumber);
    Task<IEnumerable<PurchaseReceipt>> GetAllAsync();
    Task AddAsync(PurchaseReceipt receipt);
    Task UpdateAsync(PurchaseReceipt receipt);
}
