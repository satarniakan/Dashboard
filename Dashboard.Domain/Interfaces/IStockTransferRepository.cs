using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockTransferRepository
{
    Task<StockTransfer?> GetByIdAsync(int id);
    Task<IEnumerable<StockTransfer>> GetAllAsync();
    Task AddAsync(StockTransfer transfer);
    Task UpdateAsync(StockTransfer transfer);
}
