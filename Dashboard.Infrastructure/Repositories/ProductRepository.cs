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
        await _context.Products.FindAsync(id);

    public async Task<IEnumerable<Product>> GetAllAsync() =>
        await _context.Products.ToListAsync();

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();
    }
}