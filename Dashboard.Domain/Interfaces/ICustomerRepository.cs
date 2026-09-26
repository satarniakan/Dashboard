using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id);

    /// <summary>مشتری با شماره موبایل — برای پیوند سفارش آنلاین به مشتری موجود</summary>
    Task<Customer?> GetByPhoneAsync(string phone);

    Task<IEnumerable<Customer>> GetAllAsync();
    Task AddAsync(Customer customer);
}