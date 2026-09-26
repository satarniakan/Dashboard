// Dashboard.Domain/Interfaces/INotificationRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int id);

    /// <summary>آخرین اعلان‌های کاربر (جدیدترین اول)</summary>
    Task<List<Notification>> GetForUserAsync(string userId, int take = 20, bool? onlyUnread = null);

    Task<int> GetUnreadCountAsync(string userId);

    Task AddAsync(Notification notification);

    Task UpdateAsync(Notification notification);

    /// <summary>علامت‌گذاری همه‌ی اعلان‌های نخوانده‌ی کاربر به‌عنوان خوانده‌شده</summary>
    Task MarkAllReadAsync(string userId);

    /// <summary>شناسه‌ی همه‌ی کاربران — برای اعلان سراسری</summary>
    Task<List<string>> GetAllUserIdsAsync();

    /// <summary>شناسه‌ی کاربران دارای یک نقش (نام نقش) — برای اعلان هدفمند</summary>
    Task<List<string>> GetUserIdsInRoleAsync(string roleName);
}
