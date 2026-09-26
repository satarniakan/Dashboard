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

    /// <summary>
    /// claim اتمیک سفارش برای پرداخت: فقط اگر هنوز PendingPayment است آن را Paid/PaidAt می‌کند.
    /// خروجی false یعنی درخواست همزمان دیگری زودتر پردازش کرده (یا سفارش لغو شده) —
    /// محافظ idempotency در برابر callback تکراری/موازی درگاه پرداخت.
    /// </summary>
    Task<bool> TryClaimForPaymentAsync(int orderId);

    /// <summary>وضعیت فعلی سفارش مستقیم از دیتابیس (بدون کشِ change tracker) — null یعنی سفارش وجود ندارد</summary>
    Task<Domain.Enums.OrderStatus?> GetStatusAsync(int orderId);

    /// <summary>سفارش‌های PendingPayment قدیمی‌تر از عمر مشخص — برای انقضای خودکار</summary>
    Task<List<Order>> GetStalePendingPaymentAsync(TimeSpan maxAge);

    /// <summary>
    /// تعداد دفعاتی که این کاربر از این کد تخفیف استفاده کرده (سفارش‌های لغوشده حذف می‌شوند)
    /// — برای اعمال سقف «مصرف هر مشتری» (MaxUsagePerCustomer)
    /// </summary>
    Task<int> CountUserDiscountUsagesAsync(string userId, string discountCode);
}
