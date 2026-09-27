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

    /// <summary>
    /// claim اتمیک یک پیام: فقط اگر هنوز Pending است و به سقف تلاش نرسیده، آن را
    /// Processing و Attempts+1 می‌کند. false یعنی پردازشگر/instance دیگری زودتر آن را برداشته.
    /// </summary>
    Task<bool> TryClaimForSendingAsync(int messageId, int maxAttempts);

    /// <summary>
    /// نتیجهٔ ارسال را فقط روی رکوردی که هنوز Processing است می‌نویسد:
    /// موفق → Sent؛ ناموفق تا سقف → Failed؛ ناموفقِ زیر سقف → برمی‌گردد به Pending برای تلاش بعدی.
    /// </summary>
    Task FinishSendingAsync(int messageId, bool success, int attempts, int maxAttempts, string? error, DateTime now);

    /// <summary>
    /// رکوردهای مانده در Processing (کرش پروسه وسط ارسال) را به Pending برمی‌گرداند.
    /// فقط در ابتدای دور پردازش صدا زده می‌شود، وقتی هیچ ارسالِ جاری‌ای در همین instance نیست.
    /// </summary>
    Task<int> ReclaimAbandonedAsync();
}
