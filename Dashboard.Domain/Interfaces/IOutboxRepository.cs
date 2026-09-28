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
    /// Processing و Attempts+1 می‌کند و <paramref name="now"/> را به‌عنوان «شروع قفل» ثبت می‌کند.
    /// false یعنی پردازشگر/instance دیگری زودتر آن را برداشته.
    /// </summary>
    Task<bool> TryClaimForSendingAsync(int messageId, int maxAttempts, DateTime now);

    /// <summary>
    /// نتیجهٔ ارسال را فقط روی رکوردی که هنوز Processing است می‌نویسد:
    /// موفق → Sent؛ ناموفق تا سقف → Failed؛ ناموفقِ زیر سقف → برمی‌گردد به Pending برای تلاش بعدی.
    /// </summary>
    Task FinishSendingAsync(int messageId, bool success, int attempts, int maxAttempts, string? error, DateTime now);

    /// <summary>
    /// رکوردهایی که «خیلی» بیشتر از <paramref name="lease"/> در Processing مانده‌اند را به Pending
    /// برمی‌گرداند (کرش پروسه وسط ارسال). آستانهٔ زمانی شرط لازم است: در استقرار چندنمونه‌ای،
    /// رکورد Processingِ تازه یعنی «instance دیگری همین حالا در حال ارسال است» و بازپس‌گیریِ
    /// بی‌آستانه باعث ارسال دوبارهٔ همان پیامک/ایمیل می‌شود.
    /// </summary>
    Task<int> ReclaimAbandonedAsync(TimeSpan lease);
}
