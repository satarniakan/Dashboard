using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IPurchaseReturnRepository
{
    Task<PurchaseReturn?> GetByIdAsync(int id);
    Task<IEnumerable<PurchaseReturn>> GetAllAsync();
    Task AddAsync(PurchaseReturn purchaseReturn);
    Task UpdateAsync(PurchaseReturn purchaseReturn);
    Task<List<PurchaseReturn>> GetByPurchaseReceiptIdAsync(int purchaseReceiptId);
}
