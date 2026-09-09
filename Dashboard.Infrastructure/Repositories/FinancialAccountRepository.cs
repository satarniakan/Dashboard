using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class FinancialAccountRepository : IFinancialAccountRepository
{
    private readonly AppDbContext _context;
    public FinancialAccountRepository(AppDbContext context) => _context = context;

    public async Task<FinancialAccount?> GetByIdAsync(int id) =>
        await _context.FinancialAccounts.Include(f => f.Account).FirstOrDefaultAsync(f => f.Id == id);

    public async Task<IEnumerable<FinancialAccount>> GetAllAsync() =>
        await _context.FinancialAccounts.Where(f => f.IsActive).Include(f => f.Account).ToListAsync();

    public async Task AddAsync(FinancialAccount account) =>
        await _context.FinancialAccounts.AddAsync(account);
}