using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IFinancialAccountRepository
{
    Task<FinancialAccount?> GetByIdAsync(int id);
    Task<IEnumerable<FinancialAccount>> GetAllAsync();
    Task AddAsync(FinancialAccount account);
}