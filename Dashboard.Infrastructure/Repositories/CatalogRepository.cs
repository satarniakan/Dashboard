using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _context;
    public CatalogRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Category>> GetCategoriesAsync() =>
        await _context.Categories.Where(c => c.IsActive).ToListAsync();

    public async Task AddCategoryAsync(Category category) =>
        await _context.Categories.AddAsync(category);

    public async Task<IEnumerable<ProductAttribute>> GetAttributesAsync() =>
        await _context.ProductAttributes.Include(a => a.Values).ToListAsync();

    public async Task AddAttributeAsync(ProductAttribute attribute) =>
        await _context.ProductAttributes.AddAsync(attribute);

    public async Task<ProductGroup?> GetGroupByIdAsync(int id) =>
        await _context.ProductGroups
            .Include(g => g.Category)
            .Include(g => g.Images)
            .Include(g => g.Variants).ThenInclude(v => v.ProductGroup)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<ProductGroup?> GetGroupBySlugAsync(string slug) =>
        await _context.ProductGroups
            .Include(g => g.Category)
            .Include(g => g.Images)
            .Include(g => g.Variants)
            .FirstOrDefaultAsync(g => g.Slug == slug && g.IsActive);

    public async Task<IEnumerable<ProductGroup>> GetGroupsAsync(int? categoryId = null)
    {
        var query = _context.ProductGroups.Include(g => g.Images).Include(g => g.Variants).Where(g => g.IsActive);
        if (categoryId.HasValue)
        {
            query = query.Where(g => g.CategoryId == categoryId.Value);
        }
        return await query.ToListAsync();
    }

    public async Task AddGroupAsync(ProductGroup group) =>
        await _context.ProductGroups.AddAsync(group);

    public async Task AddVariantAttributeAsync(ProductVariantAttribute variantAttribute) =>
        await _context.ProductVariantAttributes.AddAsync(variantAttribute);

    public async Task AddImageAsync(ProductImage image) =>
        await _context.ProductImages.AddAsync(image);

    public async Task<IEnumerable<Unit>> GetUnitsAsync() =>
        await _context.Units.OrderBy(u => u.Name).ToListAsync();

    public async Task<Unit?> GetUnitByIdAsync(int id) =>
        await _context.Units.FindAsync(id);

    public async Task<Unit?> GetUnitByNameAsync(string name) =>
        await _context.Units.FirstOrDefaultAsync(u => u.Name == name);

    public async Task AddUnitAsync(Unit unit) =>
        await _context.Units.AddAsync(unit);

    public void RemoveUnit(Unit unit) =>
        _context.Units.Remove(unit);
}