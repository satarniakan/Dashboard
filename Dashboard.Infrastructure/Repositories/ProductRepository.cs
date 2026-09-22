// Dashboard.Infrastructure/Repositories/ProductRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<Product?> GetByIdAsync(int id) =>
        await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Product>> GetAllAsync() =>
        await _context.Products.Include(p => p.Category).ToListAsync();

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var query = _context.Products.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));

        var totalCount = await query.CountAsync();

        // ترتیب مشخص (OrderBy) لازم است چون بدون آن، نتیجه‌ی Skip/Take در SQL تضمین‌شده نیست
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
        //await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        //await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is not null)
        {
            _context.Products.Remove(product);
            //await _context.SaveChangesAsync();
        }
    }
}