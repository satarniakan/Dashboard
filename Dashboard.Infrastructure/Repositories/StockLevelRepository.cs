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
}
