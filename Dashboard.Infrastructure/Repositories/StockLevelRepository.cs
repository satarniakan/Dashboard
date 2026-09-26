using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class StockLevelRepository : IStockLevelRepository
{
    private readonly AppDbContext _context;
    public StockLevelRepository(AppDbContext context) => _context = context;

    public async Task<StockLevel?> GetAsync(int productId, int warehouseId) =>
        await _context.StockLevels
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

    public async Task<IEnumerable<StockLevel>> GetByWarehouseAsync(int warehouseId) =>
        await _context.StockLevels
            .Include(s => s.Product)
            .Where(s => s.WarehouseId == warehouseId)
            .ToListAsync();

    public async Task<IEnumerable<StockLevel>> GetByProductAsync(int productId) =>
        await _context.StockLevels
            .Include(s => s.Warehouse)
            .Where(s => s.ProductId == productId)
            .ToListAsync();

    public async Task<IEnumerable<StockLevel>> GetAllAsync() =>
        await _context.StockLevels
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .ToListAsync();

    public async Task<IEnumerable<StockLevel>> GetBelowReorderPointAsync() =>
        await _context.StockLevels
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .Where(s => s.Product != null && s.QuantityOnHand < s.Product.ReorderPoint)
            .ToListAsync();

    public async Task IncreaseOrCreateAsync(int productId, int warehouseId, decimal delta)
    {
        var level = await _context.StockLevels
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        if (level is null)
        {
            level = new StockLevel
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                QuantityOnHand = delta,
                LastUpdatedAt = DateTime.UtcNow
            };
            await _context.StockLevels.AddAsync(level);
        }
        else
        {
            level.QuantityOnHand += delta;
            level.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// کاهش موجودی با بررسی کفایت — اگر موجودی کافی نباشد استثنا پرتاب می‌شود
    /// </summary>
    public async Task DecreaseWithCheckAsync(int productId, int warehouseId, decimal quantity)
    {
        var level = await _context.StockLevels
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        if (level is null || level.QuantityOnHand < quantity)
        {
            var productName = level?.Product?.Name ?? $"شماره {productId}";
            var available = level?.QuantityOnHand ?? 0;
            throw new Dashboard.Domain.Exceptions.BusinessRuleException(
                $"موجودی کافی نیست (کالای «{productName}»: موجود {available}, درخواستی {quantity}).");
        }

        level.QuantityOnHand -= quantity;
        level.LastUpdatedAt = DateTime.UtcNow;
    }

    public async Task<Dictionary<int, decimal>> GetTotalStockAsync(IReadOnlyCollection<int> productIds)
    {
        if (productIds.Count == 0) return new Dictionary<int, decimal>();

        return await _context.StockLevels
            .Where(sl => productIds.Contains(sl.ProductId))
            .GroupBy(sl => sl.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.QuantityOnHand) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total);
    }
}
