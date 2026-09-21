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

    /// <summary>
    /// کاهش موجودی با بررسی کفایت — اگر موجودی کافی نباشد استثنا پرتاب می‌شود.
    /// این متد به همراه RowVersion (همزمانی خوش‌بینانه) از race condition بین چند درخواست همزمان جلوگیری می‌کند.
    /// </summary>
    Task DecreaseWithCheckAsync(int productId, int warehouseId, decimal quantity);
}
