using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockLevelRepository
{
    Task<StockLevel?> GetAsync(int productId, int warehouseId);
    Task<IEnumerable<StockLevel>> GetByWarehouseAsync(int warehouseId);
    Task<IEnumerable<StockLevel>> GetByProductAsync(int productId);
    Task<IEnumerable<StockLevel>> GetAllAsync();

    /// کالاهایی که موجودی‌شان از نقطه‌ی سفارش کمتر شده (در کل انبارها)
    Task<IEnumerable<StockLevel>> GetBelowReorderPointAsync();

    /// اگر رکورد (کالا، انبار) وجود نداشت می‌سازد، در غیر این صورت مقدار را می‌افزاید (می‌تواند منفی باشد)
    Task IncreaseOrCreateAsync(int productId, int warehouseId, decimal delta);
}
