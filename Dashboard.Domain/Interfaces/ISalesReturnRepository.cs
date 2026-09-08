using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ISalesReturnRepository
{
    Task<SalesReturn?> GetByIdAsync(int id);
    Task<IEnumerable<SalesReturn>> GetAllAsync();
    Task AddAsync(SalesReturn salesReturn);
}
