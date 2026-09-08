using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockCountRepository
{
    Task<StockCount?> GetByIdAsync(int id);
    Task<IEnumerable<StockCount>> GetAllAsync();
    Task AddAsync(StockCount stockCount);
    Task UpdateAsync(StockCount stockCount);
}
