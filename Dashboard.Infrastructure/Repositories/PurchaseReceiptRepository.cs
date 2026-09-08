using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class PurchaseReceiptRepository : IPurchaseReceiptRepository
{
    private readonly AppDbContext _context;
    public PurchaseReceiptRepository(AppDbContext context) => _context = context;

    public async Task<PurchaseReceipt?> GetByIdAsync(int id) =>
        await _context.PurchaseReceipts
            .Include(r => r.Items).ThenInclude(i => i.Product)
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<PurchaseReceipt>> GetAllAsync() =>
        await _context.PurchaseReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();

    public async Task AddAsync(PurchaseReceipt receipt) =>
        await _context.PurchaseReceipts.AddAsync(receipt);
}
