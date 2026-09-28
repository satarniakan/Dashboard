// Dashboard.Domain/Entities/OutboxMessage.cs
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>
/// صف خروجی پیام‌ها (الگوی Outbox) — هم پیامک و هم ایمیل.
/// کدها فقط رکورد می‌سازند تا وابستگی به سرویس بیرونی جریان اصلی را نشکند؛
/// OutboxProcessor صف را می‌فرستد.
/// </summary>
public class OutboxMessage
{
    public int Id { get; set; }

    public OutboxChannel Channel { get; set; } = OutboxChannel.Sms;

    /// <summary>شماره موبایل (پیامک) یا آدرس ایمیل</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>فقط برای ایمیل</summary>
    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>تعداد تلاش‌های ارسال — پس از سقف، Failed دائمی می‌شود</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// لحظهٔ claim (ورود به Processing). ستونِ جدا است چون «چه کسی این پیام را در دست دارد»
    /// در دیتابیس ثبت نمی‌شود: بدون این زمان، بازپس‌گیری رکوردهای Processing نمی‌تواند
    /// «کرش واقعی» را از «ارسال جاریِ یک instance دیگر» تشخیص دهد و پیام دوباره ارسال می‌شد.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }
}
