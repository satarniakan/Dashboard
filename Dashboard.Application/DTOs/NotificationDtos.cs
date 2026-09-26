using System.ComponentModel.DataAnnotations;
using Dashboard.Domain.Enums;

namespace Dashboard.Application.DTOs;
// Dashboard.Application/DTOs/NotificationDtos.cs



public record NotificationDto(
    int Id,
    string Title,
    string? Body,
    NotificationType Type,
    string? LinkUrl,
    DateTime CreatedAt)
{
    /// <summary>قابل تغییر در UI (علامت‌گذاری خوانده‌شده بدون رفرش)</summary>
    public bool IsRead { get; set; }
}

public record NotificationFeedDto(
    int UnreadCount,
    IReadOnlyList<NotificationDto> Items);


public class BroadcastDto
{
    [Required(ErrorMessage = "عنوان الزامی است.")]
    [StringLength(200, ErrorMessage = "عنوان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "متن نمی‌تواند بیشتر از ۱٬۰۰۰ کاراکتر باشد.")]
    public string? Body { get; set; }

    /// <summary>null = همه‌ی کاربران؛ otherwise نام نقش</summary>
    public string? RoleName { get; set; }

    public NotificationType Type { get; set; } = NotificationType.System;

    [StringLength(300)]
    public string? LinkUrl { get; set; }
}
