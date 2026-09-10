using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ICatalogRepository
{
    // دسته‌بندی
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task AddCategoryAsync(Category category);

    // ویژگی‌ها
    Task<IEnumerable<ProductAttribute>> GetAttributesAsync();
    Task AddAttributeAsync(ProductAttribute attribute);

    // گروه محصول
    Task<ProductGroup?> GetGroupByIdAsync(int id);
    Task<ProductGroup?> GetGroupBySlugAsync(string slug);
    Task<IEnumerable<ProductGroup>> GetGroupsAsync(int? categoryId = null);
    Task AddGroupAsync(ProductGroup group);

    Task AddVariantAttributeAsync(ProductVariantAttribute variantAttribute);
    Task AddImageAsync(ProductImage image);
}