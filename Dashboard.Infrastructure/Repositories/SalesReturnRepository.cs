using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class SalesReturnRepository : ISalesReturnRepository
{
    private readonly AppDbContext _context;
    public SalesReturnRepository(AppDbContext context) => _context = context;

    public async Task<SalesReturn?> GetByIdAsync(int id) =>
        await _context.SalesReturns
            .Include(r => r.Items).ThenInclude(x => x.Product)
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<SalesReturn>> GetAllAsync() =>
        await _context.SalesReturns
            .Include(r => r.Items)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();

    public async Task AddAsync(SalesReturn salesReturn) =>
        await _context.SalesReturns.AddAsync(salesReturn);
    public Task UpdateAsync(SalesReturn receipt)
    {
        _context.SalesReturns.Update(receipt);
        return Task.CompletedTask;
    }
    public async Task<List<SalesReturn>> GetBySalesInvoiceIdAsync(int salesInvoiceId)
    {
        return await _context.SalesReturns
            .Include(x => x.Items)
            .Where(x => x.SalesInvoiceId == salesInvoiceId)
            .ToListAsync();
    }
}
