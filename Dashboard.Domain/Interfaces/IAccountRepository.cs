using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(int id);
    Task<Account?> GetByCodeAsync(string code);
    Task<IEnumerable<Account>> GetAllAsync();
    Task AddAsync(Account account);
}