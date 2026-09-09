using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;
    public CustomerRepository(AppDbContext context) => _context = context;

    public async Task<Customer?> GetByIdAsync(int id) =>
        await _context.Customers.FindAsync(id);

    public async Task<IEnumerable<Customer>> GetAllAsync() =>
        await _context.Customers.Where(c => c.IsActive).ToListAsync();

    public async Task AddAsync(Customer customer) =>
        await _context.Customers.AddAsync(customer);
}
