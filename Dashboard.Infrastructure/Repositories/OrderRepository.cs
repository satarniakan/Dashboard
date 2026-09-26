// Dashboard.Infrastructure/Repositories/OrderRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public async Task<Order?> GetByIdAsync(int id) =>
        await _context.Orders.FindAsync(id);

    public async Task<Order?> GetByIdWithDetailsAsync(int id) =>
        await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IEnumerable<Order>> GetByUserAsync(string userId) =>
        await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<(IEnumerable<Order> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, OrderStatus? status = null, string? search = null)
    {
        var query = _context.Orders.AsQueryable();

        if (status is not null)
            query = query.Where(o => o.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o => o.OrderNumber.Contains(search) || o.CustomerName.Contains(search) || o.CustomerPhone.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Order?> GetByAuthorityAsync(string authority) =>
        await _context.Orders
            .Include(o => o.Payments)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Payments.Any(p => p.Authority == authority));

    public async Task AddAsync(Order order) =>
        await _context.Orders.AddAsync(order);

    public Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        return Task.CompletedTask;
    }

    public async Task<bool> TryClaimForPaymentAsync(int orderId)
    {
        var paidAt = DateTime.UtcNow;
        var rows = await _context.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, paidAt));
        return rows > 0;
    }

    public async Task<OrderStatus?> GetStatusAsync(int orderId) =>
        await _context.Orders
            .Where(o => o.Id == orderId)
            .Select(o => (OrderStatus?)o.Status)
            .FirstOrDefaultAsync();

    public async Task<List<Order>> GetStalePendingPaymentAsync(TimeSpan maxAge)
    {
        // محاسبه‌ی مرز زمانی بیرون از عبارت — EF تفریق TimeSpan با پارامتر را ترجمه نمی‌کند
        var cutoff = DateTime.UtcNow - maxAge;

        // سفارش‌هایی که پرداخت‌شان موفق ثبت شده ولی خطای سیستمی خورده، لغو خودکار نشوند
        return await _context.Orders
            .Where(o => o.Status == OrderStatus.PendingPayment
                     && o.CreatedAt < cutoff
                     && !o.Payments.Any(pmt => pmt.Status == Domain.Entities.PaymentStatus.Success))
            .ToListAsync();
    }

    public async Task<int> CountUserDiscountUsagesAsync(string userId, string discountCode)
    {
        // سفارش لغوشده سهمیهٔ مصرف را آزاد می‌کند؛ سفارش در انتظار پرداخت هم شمرده می‌شود
        return await _context.Orders
            .CountAsync(o => o.UserId == userId
                          && o.DiscountCodeText == discountCode
                          && o.DiscountAmount > 0
                          && o.Status != OrderStatus.Canceled);
    }
}
