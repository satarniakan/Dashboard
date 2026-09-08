using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _context;
    public SupplierRepository(AppDbContext context) => _context = context;

    public async Task<Supplier?> GetByIdAsync(int id) =>
        await _context.Suppliers.FindAsync(id);

    public async Task<IEnumerable<Supplier>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Suppliers.AsQueryable();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task AddAsync(Supplier supplier) =>
        await _context.Suppliers.AddAsync(supplier);

    public Task UpdateAsync(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
        return Task.CompletedTask;
    }
}
