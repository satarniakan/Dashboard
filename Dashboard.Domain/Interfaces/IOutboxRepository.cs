// Dashboard.Domain/Interfaces/IOutboxRepository.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Interfaces;

public interface IOutboxRepository
{
    /// <summary>پیام‌های در انتظار ارسال یک کانال که به سقف تلاش نرسیده‌اند</summary>
    Task<List<OutboxMessage>> GetPendingAsync(OutboxChannel channel, int maxAttempts, int take);

    Task AddAsync(OutboxMessage message);

    Task UpdateAsync(OutboxMessage message);
}
