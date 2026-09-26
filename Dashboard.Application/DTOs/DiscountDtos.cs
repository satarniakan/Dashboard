// Dashboard.Application/DTOs/DiscountDtos.cs
using System.ComponentModel.DataAnnotations;
using Dashboard.Domain.Enums;

namespace Dashboard.Application.DTOs;

public class CreateDiscountCodeDto
{
    [Required(ErrorMessage = "کد تخفیف الزامی است.")]
    [StringLength(50, ErrorMessage = "کد تخفیف نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    [Range(0.01, double.MaxValue, ErrorMessage = "مقدار تخفیف باید بزرگتر از صفر باشد.")]
    public decimal Value { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "سقف تخفیف نمی‌تواند منفی باشد.")]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "حداقل سبد نمی‌تواند منفی باشد.")]
    public decimal? MinCartAmount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "سقف مصرف باید حداقل ۱ باشد.")]
    public int? MaxUsageCount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public record DiscountCodeDto(
    int Id,
    string Code,
    DiscountType Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal? MinCartAmount,
    int? MaxUsageCount,
    int UsageCount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    bool IsActive);
