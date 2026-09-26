// Dashboard.Domain/Interfaces/IProductRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();

    /// <summary>کالاهای مشخص با دسته‌بندی — جایگزین GetAllAsync برای سبد/سفارش</summary>
    Task<List<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids);

    // page از ۱ شروع می‌شود؛ search اختیاری است (جست‌وجو در نام کالا)
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);

    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);

    // برای مدیریت واحد شمارش: کالاهایی که از یک واحد مشخص استفاده می‌کنند (ویرایش/حذفِ آبشاری)
    Task<List<Product>> GetByUnitNameAsync(string unitName);
    Task<List<string>> GetAllUnitNamesAsync();

    // --- فروشگاه اینترنتی ---
    // کالای با این Slug (منتشرشده یا نه — برای اعتبارسنجی یکتایی)
    Task<Product?> GetBySlugAsync(string slug);

    // کالای منتشرشده برای صفحه‌ی عمومی محصول
    Task<Product?> GetPublishedBySlugAsync(string slug);

    // فهرست عمومی ویترین با جست‌وجو/دسته‌بندی و صفحه‌بندی
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPublishedPagedAsync(int page, int pageSize, string? search = null, int? categoryId = null);

    // جدیدترین کالاهای منتشرشده برای صفحه‌ی اصلی فروشگاه
    Task<IEnumerable<Product>> GetLatestPublishedAsync(int count);
}