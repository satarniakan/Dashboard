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
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

    // AsNoTracking لازم است تا EF نسخهٔ کهنهٔ موجودی را از change tracker برنگرداند
    // و مبنای محاسبات تصمیم‌ساز همیشه مقدارِ واقعیِ دیتابیس باشد.
    public async Task<decimal?> GetOnHandQuantityAsync(int productId, int warehouseId) =>
        await _context.StockLevels
            .AsNoTracking()
            .Where(s => s.ProductId == productId && s.WarehouseId == warehouseId)
            .Select(s => (decimal?)s.QuantityOnHand)
            .FirstOrDefaultAsync();

    public async Task<Dictionary<int, decimal>> GetWarehouseOnHandAsync(int warehouseId) =>
        await _context.StockLevels
            .AsNoTracking()
            .Where(s => s.WarehouseId == warehouseId)
            .ToDictionaryAsync(s => s.ProductId, s => s.QuantityOnHand);

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

    public async Task<Dictionary<int, decimal>> GetWarehouseStockAsync(IReadOnlyCollection<int> productIds, int warehouseId)
    {
        if (productIds.Count == 0) return new Dictionary<int, decimal>();

        return await _context.StockLevels
            .Where(sl => sl.WarehouseId == warehouseId && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, sl => sl.QuantityOnHand);
    }

    public async Task<Dictionary<int, decimal>> GetAvailableForSaleAsync(IReadOnlyCollection<int> productIds, int warehouseId)
    {
        if (productIds.Count == 0) return new Dictionary<int, decimal>();

        return await _context.StockLevels
            .Where(sl => sl.WarehouseId == warehouseId && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, sl => sl.QuantityOnHand - sl.ReservedQuantity);
    }

    /// <summary>
    /// رزرو اتمیک: شرط و به‌روزرسانی در یک UPDATE انجام می‌شود، پس دو درخواست همزمان
    /// نمی‌توانند بیش از موجودیِ آزاد رزرو کنند.
    /// </summary>
    public async Task<bool> TryReserveAsync(int productId, int warehouseId, decimal quantity)
    {
        if (quantity <= 0) return true;

        var now = DateTime.UtcNow;
        var rows = await _context.StockLevels
            .Where(s => s.ProductId == productId
                     && s.WarehouseId == warehouseId
                     && s.QuantityOnHand - s.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity + quantity)
                .SetProperty(x => x.LastUpdatedAt, now));

        return rows > 0;
    }

    public async Task ReleaseReservationAsync(int productId, int warehouseId, decimal quantity)
    {
        if (quantity <= 0) return;

        var now = DateTime.UtcNow;
        await _context.StockLevels
            .Where(s => s.ProductId == productId
                     && s.WarehouseId == warehouseId
                     && s.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - quantity)
                .SetProperty(x => x.LastUpdatedAt, now));
    }

    public async Task<List<(int ProductId, decimal Reserved, decimal OnHand)>> GetIdleReservedLevelsAsync(
        int warehouseId, DateTime notUpdatedAfter)
    {
        var rows = await _context.StockLevels
            .Where(s => s.WarehouseId == warehouseId
                     && s.ReservedQuantity > 0
                     && s.LastUpdatedAt <= notUpdatedAfter)
            .Select(s => new { s.ProductId, s.ReservedQuantity, s.QuantityOnHand })
            .ToListAsync();

        return rows.Select(r => (r.ProductId, r.ReservedQuantity, r.QuantityOnHand)).ToList();
    }

    public async Task<bool> AlignReservedQuantityAsync(int productId, int warehouseId, decimal expected, DateTime now)
    {
        // شرط QuantityOnHand >= expected از نقض CK_StockLevels_ReservedValid جلوگیری می‌کند؛
        // شرط ReservedQuantity > expected هم نگه می‌دارد که این عملیات هرگز رزرو را افزایش ندهد.
        var rows = await _context.StockLevels
            .Where(s => s.ProductId == productId
                     && s.WarehouseId == warehouseId
                     && s.ReservedQuantity > expected
                     && s.QuantityOnHand >= expected)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, expected)
                .SetProperty(x => x.LastUpdatedAt, now));
        return rows > 0;
    }
}
