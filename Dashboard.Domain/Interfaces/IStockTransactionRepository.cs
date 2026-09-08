using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockTransactionRepository
{
    Task AddAsync(StockTransaction transaction);
    Task<IEnumerable<StockTransaction>> GetHistoryAsync(int? productId = null, int? warehouseId = null, int take = 200);
}
