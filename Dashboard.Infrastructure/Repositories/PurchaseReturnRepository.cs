using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly AppDbContext _context;
    public PurchaseReturnRepository(AppDbContext context) => _context = context;

    public async Task<PurchaseReturn?> GetByIdAsync(int id) =>
        await _context.PurchaseReturns
            .Include(r => r.Items).ThenInclude(x => x.Product)
            .Include(r => r.Warehouse)
            .Include(r => r.PurchaseReceipt)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<PurchaseReturn>> GetAllAsync() =>
        await _context.PurchaseReturns
            .Include(r => r.Items)
            .Include(r => r.Warehouse)
            .Include(r => r.PurchaseReceipt)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();

    public async Task AddAsync(PurchaseReturn purchaseReturn) =>
        await _context.PurchaseReturns.AddAsync(purchaseReturn);

    public Task UpdateAsync(PurchaseReturn purchaseReturn)
    {
        _context.PurchaseReturns.Update(purchaseReturn);
        return Task.CompletedTask;
    }

    public async Task<List<PurchaseReturn>> GetByPurchaseReceiptIdAsync(int purchaseReceiptId)
    {
        return await _context.PurchaseReturns
            .Include(x => x.Items)
            .Where(x => x.PurchaseReceiptId == purchaseReceiptId)
            .ToListAsync();
    }
}
