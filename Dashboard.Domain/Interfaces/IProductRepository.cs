// Dashboard.Domain/Interfaces/IProductRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}