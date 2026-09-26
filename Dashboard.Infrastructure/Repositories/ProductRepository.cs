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

    public async Task<List<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return new List<Product>();
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();
    }

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

    public async Task<Product?> GetBySlugAsync(string slug) =>
        await _context.Products.FirstOrDefaultAsync(p => p.Slug == slug);

    public async Task<Product?> GetPublishedBySlugAsync(string slug) =>
        await _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductGroup)
                .ThenInclude(g => g!.Images)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPublishedPagedAsync(int page, int pageSize, string? search = null, int? categoryId = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsPublished)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        if (categoryId is not null)
            query = query.Where(p => p.CategoryId == categoryId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Product>> GetLatestPublishedAsync(int count) =>
        await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<List<Product>> GetByUnitNameAsync(string unitName) =>
        await _context.Products.Where(p => p.Unit == unitName).ToListAsync();

    public async Task<List<string>> GetAllUnitNamesAsync() =>
        await _context.Products.Select(p => p.Unit).ToListAsync();
}