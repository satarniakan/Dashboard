// Dashboard.Domain/Entities/Notification.cs
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>اعلان درون‌برنامه‌ای برای یک کاربر (زنگ notifier در پنل)</summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>شناسه‌ی کاربر Identity (رشته‌ی GUID)</summary>
    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    public NotificationType Type { get; set; } = NotificationType.System;

    /// <summary>لینک داخلی برای پرش مستقیم (مثلاً /shop/orders/5)</summary>
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
