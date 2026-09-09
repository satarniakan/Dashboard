using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class CustomerReceiptRepository : ICustomerReceiptRepository
{
    private readonly AppDbContext _context;
    public CustomerReceiptRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<CustomerReceipt>> GetAllAsync() =>
        await _context.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.FinancialAccount)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();

    public async Task AddAsync(CustomerReceipt receipt) =>
        await _context.CustomerReceipts.AddAsync(receipt);
}