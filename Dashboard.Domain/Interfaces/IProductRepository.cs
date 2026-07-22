// Dashboard.Domain/Interfaces/IProductRepository.cs
namespace Dashboard.Domain.Interfaces;

using Dashboard.Domain.Entities;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task AddAsync(Product product);
}