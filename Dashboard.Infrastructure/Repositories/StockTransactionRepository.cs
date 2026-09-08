using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class StockTransactionRepository : IStockTransactionRepository
{
    private readonly AppDbContext _context;
    public StockTransactionRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(StockTransaction transaction) =>
        await _context.StockTransactions.AddAsync(transaction);

    public async Task<IEnumerable<StockTransaction>> GetHistoryAsync(int? productId = null, int? warehouseId = null, int take = 200)
    {
        var query = _context.StockTransactions
            .Include(t => t.Product)
            .Include(t => t.Warehouse)
            .AsQueryable();

        if (productId.HasValue) query = query.Where(t => t.ProductId == productId.Value);
        if (warehouseId.HasValue) query = query.Where(t => t.WarehouseId == warehouseId.Value);

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .Take(take)
            .ToListAsync();
    }
}
