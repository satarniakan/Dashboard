using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record CustomerDto(int Id, string Name, string? Phone, string? Address);

public class CreateCustomerDto
{
    [Required(ErrorMessage = "نام مشتری الزامی است.")]
    [StringLength(150, ErrorMessage = "نام مشتری نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    // ارقام فارسی/عربی و نویسه‌های متعارف شماره تلفن را می‌پذیرد؛ مقدار خالی هم مجاز است (فیلد اختیاری).
    [RegularExpression(@"^(?:[0-9٠-٩۰-۹+\-\s\(\)\.]{7,})?$",
        ErrorMessage = "شماره تلفن معتبر نیست.")]
    [StringLength(50, ErrorMessage = "تلفن نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "آدرس نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Address { get; set; }
}