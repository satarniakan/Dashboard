// Dashboard.Domain/Interfaces/IOrderRepository.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);

    /// <summary>با اقلام و پرداخت‌ها</summary>
    Task<Order?> GetByIdWithDetailsAsync(int id);

    Task<IEnumerable<Order>> GetByUserAsync(string userId);

    Task<(IEnumerable<Order> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, OrderStatus? status = null, string? search = null);

    Task<Order?> GetByAuthorityAsync(string authority);

    Task AddAsync(Order order);

    Task UpdateAsync(Order order);

    /// <summary>سفارش‌های PendingPayment قدیمی‌تر از عمر مشخص — برای انقضای خودکار</summary>
    Task<List<Order>> GetStalePendingPaymentAsync(TimeSpan maxAge);
}
