using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class StockTransferRepository : IStockTransferRepository
{
    private readonly AppDbContext _context;
    public StockTransferRepository(AppDbContext context) => _context = context;

    public async Task<StockTransfer?> GetByIdAsync(int id) =>
        await _context.StockTransfers
            .Include(t => t.Items).ThenInclude(x => x.Product)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<StockTransfer>> GetAllAsync() =>
        await _context.StockTransfers
            .Include(t => t.Items)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

    public async Task AddAsync(StockTransfer transfer) =>
        await _context.StockTransfers.AddAsync(transfer);

    public Task UpdateAsync(StockTransfer transfer)
    {
        _context.StockTransfers.Update(transfer);
        return Task.CompletedTask;
    }
}
