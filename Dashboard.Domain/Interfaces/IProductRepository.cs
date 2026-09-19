// Dashboard.Domain/Interfaces/IProductRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();

    // page از ۱ شروع می‌شود؛ search اختیاری است (جست‌وجو در نام کالا)
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);

    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}