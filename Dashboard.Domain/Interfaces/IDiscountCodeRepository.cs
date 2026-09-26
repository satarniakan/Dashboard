// Dashboard.Domain/Interfaces/IDiscountCodeRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IDiscountCodeRepository
{
    /// <summary>کد را با حروف بزرگ نرمال‌شده می‌خواند</summary>
    Task<DiscountCode?> GetByCodeAsync(string code);

    Task<IEnumerable<DiscountCode>> GetAllAsync();

    Task<DiscountCode?> GetByIdAsync(int id);

    Task AddAsync(DiscountCode discountCode);

    Task UpdateAsync(DiscountCode discountCode);

    Task DeleteAsync(int id);
}
