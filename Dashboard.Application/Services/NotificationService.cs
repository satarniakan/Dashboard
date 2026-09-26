// Dashboard.Application/Services/NotificationService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface INotificationService
{
    /// <summary>ایجاد اعلان برای یک کاربر</summary>
    Task NotifyAsync(string userId, string title, string? body = null,
        NotificationType type = NotificationType.System, string? linkUrl = null);

    Task<NotificationFeedDto> GetFeedAsync(string userId, int take = 20);

    Task<int> GetUnreadCountAsync(string userId);

    Task MarkReadAsync(int notificationId, string userId);

    Task MarkAllReadAsync(string userId);

    /// <summary>اعلان گروهی برای فهرست کاربران</summary>
    Task NotifyUsersAsync(System.Collections.Generic.IEnumerable<string> userIds, string title, string? body,
        NotificationType type, string? linkUrl);

    /// <summary>اعلان برای اعضای یک نقش</summary>
    Task NotifyRoleAsync(string roleName, string title, string? body, NotificationType type, string? linkUrl);

    /// <summary>
    /// اگر موجودی کل کالا به نقطه‌ی سفارش رسیده باشد، به انباردار و ادمین اعلان می‌دهد.
    /// بعد از عملیات‌های کاهنده‌ی موجودی (فروش، ضایعات، حواله) فراخوانی شود.
    /// </summary>
    Task NotifyLowStockIfBelowReorderPointAsync(int productId);

    /// <summary>اعلان سراسری — به همه‌ی کاربران یا اعضای یک نقش</summary>
    Task<int> BroadcastAsync(string title, string? body, NotificationType type, string? linkUrl, string? roleName = null);
}

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task NotifyAsync(string userId, string title, string? body = null,
        NotificationType type = NotificationType.System, string? linkUrl = null)
    {
        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            LinkUrl = linkUrl
        });
        await _unitOfWork.CompleteAsync();
    }

    public async Task<NotificationFeedDto> GetFeedAsync(string userId, int take = 20)
    {
        var unreadCount = await _unitOfWork.Notifications.GetUnreadCountAsync(userId);
        var items = await _unitOfWork.Notifications.GetForUserAsync(userId, take);
        return new NotificationFeedDto(unreadCount, items.Select(ToDto).ToList());
    }

    public Task<int> GetUnreadCountAsync(string userId) =>
        _unitOfWork.Notifications.GetUnreadCountAsync(userId);

    public async Task MarkReadAsync(int notificationId, string userId)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
        if (notification is null || notification.UserId != userId || notification.IsRead)
            return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _unitOfWork.Notifications.UpdateAsync(notification);
        await _unitOfWork.CompleteAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await _unitOfWork.Notifications.MarkAllReadAsync(userId);
        await _unitOfWork.CompleteAsync();
    }

    public async Task NotifyUsersAsync(System.Collections.Generic.IEnumerable<string> userIds, string title, string? body,
        NotificationType type, string? linkUrl)
    {
        foreach (var userId in userIds)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                LinkUrl = linkUrl
            });
        }
        await _unitOfWork.CompleteAsync();
    }

    public async Task NotifyRoleAsync(string roleName, string title, string? body, NotificationType type, string? linkUrl) =>
        await NotifyUsersAsync(await _unitOfWork.Notifications.GetUserIdsInRoleAsync(roleName), title, body, type, linkUrl);

    public async Task NotifyLowStockIfBelowReorderPointAsync(int productId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product is null || product.ReorderPoint <= 0) return;

        var stock = await _unitOfWork.StockLevels.GetTotalStockAsync(new[] { productId });
        var total = stock.TryGetValue(productId, out var s) ? s : 0;
        if (total > product.ReorderPoint) return;

        var body = $"موجودی «{product.Name}» به {total:0.##} {product.Unit} رسید (نقطه سفارش: {product.ReorderPoint}).";
        await NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser, "کاهش موجودی", body, NotificationType.System, "/warehouse/stock");
        await NotifyRoleAsync(Dashboard.Domain.Identity.Roles.Admin, "کاهش موجودی", body, NotificationType.System, "/warehouse/stock");
    }

    public async Task<int> BroadcastAsync(string title, string? body, NotificationType type, string? linkUrl, string? roleName = null)
    {
        var userIds = roleName is null
            ? await _unitOfWork.Notifications.GetAllUserIdsAsync()
            : await _unitOfWork.Notifications.GetUserIdsInRoleAsync(roleName);

        // ارسال دسته‌ای در یک Save — برای تعداد زیاد کاربر هم فقط یک رفت‌وبرگشت دیتابیس
        foreach (var userId in userIds)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                LinkUrl = linkUrl
            });
        }
        await _unitOfWork.CompleteAsync();

        return userIds.Count;
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id, n.Title, n.Body, n.Type, n.LinkUrl, n.CreatedAt)
    { IsRead = n.IsRead };
}
