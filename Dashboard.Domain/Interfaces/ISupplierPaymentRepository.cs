using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ISupplierPaymentRepository
{
    Task<IEnumerable<SupplierPayment>> GetAllAsync();
    Task AddAsync(SupplierPayment payment);
}