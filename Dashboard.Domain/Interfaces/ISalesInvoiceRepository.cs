using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ISalesInvoiceRepository
{
    Task<SalesInvoice?> GetByIdAsync(int id);
    Task<IEnumerable<SalesInvoice>> GetAllAsync();
    Task AddAsync(SalesInvoice invoice);
    Task UpdateAsync(SalesInvoice invoice);
}