using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IWarehouseRepository
{
    Task<Warehouse?> GetByIdAsync(int id);
    Task<IEnumerable<Warehouse>> GetAllAsync(bool includeInactive = false);
    Task AddAsync(Warehouse warehouse);
    Task UpdateAsync(Warehouse warehouse);
}
