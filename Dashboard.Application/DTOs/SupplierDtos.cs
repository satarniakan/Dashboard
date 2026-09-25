using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record SupplierDto(int Id, string Name, string? ContactPerson, string? Phone, string? Address);

public class CreateSupplierDto
{
    [Required(ErrorMessage = "نام تأمین‌کننده الزامی است.")]
    [StringLength(150, ErrorMessage = "نام تأمین‌کننده نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "نام شخص رابط نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string? ContactPerson { get; set; }

    // ارقام فارسی/عربی و نویسه‌های متعارف شماره تلفن را می‌پذیرد؛ مقدار خالی هم مجاز است (فیلد اختیاری).
    [RegularExpression(@"^(?:[0-9٠-٩۰-۹+\-\s\(\)\.]{7,})?$",
        ErrorMessage = "شماره تلفن معتبر نیست.")]
    [StringLength(50, ErrorMessage = "تلفن نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "آدرس نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Address { get; set; }
}
