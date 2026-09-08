using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(int id);
    Task<IEnumerable<Supplier>> GetAllAsync(bool includeInactive = false);
    Task AddAsync(Supplier supplier);
    Task UpdateAsync(Supplier supplier);
}
