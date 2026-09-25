using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record WarehouseDto(int Id, string Name, string? Code, string? Address, bool IsActive);

public class CreateWarehouseDto
{
    [Required(ErrorMessage = "نام انبار الزامی است.")]
    [StringLength(150, ErrorMessage = "نام انبار نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "کد انبار نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Code { get; set; }

    [StringLength(500, ErrorMessage = "آدرس نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Address { get; set; }
}
