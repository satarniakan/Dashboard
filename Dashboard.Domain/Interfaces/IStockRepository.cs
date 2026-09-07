using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockRepository
{
    Task<ProductStock?> GetAsync(int productId, int warehouseId);
    Task<IEnumerable<ProductStock>> GetByWarehouseAsync(int warehouseId);
    Task AddOrUpdateStockAsync(int productId, int warehouseId, int quantityDelta);
    Task AddMovementAsync(StockMovement movement);
    Task<IEnumerable<StockMovement>> GetMovementsForProductAsync(int productId, int warehouseId);
}