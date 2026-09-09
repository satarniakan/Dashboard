using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ICustomerReceiptRepository
{
    Task<IEnumerable<CustomerReceipt>> GetAllAsync();
    Task AddAsync(CustomerReceipt receipt);
}