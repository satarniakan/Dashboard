using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _context;
    public WarehouseRepository(AppDbContext context) => _context = context;

    public async Task<Warehouse?> GetByIdAsync(int id) =>
        await _context.Warehouses.FindAsync(id);

    public async Task<IEnumerable<Warehouse>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Warehouses.AsQueryable();
        if (!includeInactive) query = query.Where(w => w.IsActive);
        return await query.OrderBy(w => w.Name).ToListAsync();
    }

    public async Task AddAsync(Warehouse warehouse) =>
        await _context.Warehouses.AddAsync(warehouse);

    public Task UpdateAsync(Warehouse warehouse)
    {
        _context.Warehouses.Update(warehouse);
        return Task.CompletedTask;
    }
}
