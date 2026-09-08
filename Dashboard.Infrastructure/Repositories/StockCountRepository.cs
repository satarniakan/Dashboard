using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class StockCountRepository : IStockCountRepository
{
    private readonly AppDbContext _context;
    public StockCountRepository(AppDbContext context) => _context = context;

    public async Task<StockCount?> GetByIdAsync(int id) =>
        await _context.StockCounts
            .Include(c => c.Items).ThenInclude(x => x.Product)
            .Include(c => c.Warehouse)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<StockCount>> GetAllAsync() =>
        await _context.StockCounts
            .Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync();

    public async Task AddAsync(StockCount stockCount) =>
        await _context.StockCounts.AddAsync(stockCount);

    public Task UpdateAsync(StockCount stockCount)
    {
        _context.StockCounts.Update(stockCount);
        return Task.CompletedTask;
    }
}
