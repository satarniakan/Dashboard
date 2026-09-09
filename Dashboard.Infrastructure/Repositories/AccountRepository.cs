using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;
    public AccountRepository(AppDbContext context) => _context = context;

    public async Task<Account?> GetByIdAsync(int id) =>
        await _context.Accounts.FindAsync(id);

    public async Task<Account?> GetByCodeAsync(string code) =>
        await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code);

    public async Task<IEnumerable<Account>> GetAllAsync() =>
        await _context.Accounts.Where(a => a.IsActive).OrderBy(a => a.Code).ToListAsync();

    public async Task AddAsync(Account account) =>
        await _context.Accounts.AddAsync(account);
}