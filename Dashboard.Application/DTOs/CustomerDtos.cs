using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record CustomerDto(int Id, string Name, string? Phone, string? Address);

public class CreateCustomerDto
{
    [Required(ErrorMessage = "نام مشتری الزامی است.")]
    [StringLength(150, ErrorMessage = "نام مشتری نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    // ارقام فارسی/عربی و نویسه‌های متعارف شماره تلفن را می‌پذیرد؛ مقدار خالی هم مجاز است (فیلد اختیاری).
    // سقف ۲۰ عمداً با اندازهٔ ستون Customer.Phone در دیتابیس (nvarchar(20)) هم‌خط است؛
    // در غیر این صورت مقدار ۲۱ تا ۵۰ نویسه از اعتبارسنجی رد می‌شد ولی موقع ذخیره خطای truncate می‌داد.
    [RegularExpression(@"^(?:[0-9٠-٩۰-۹+\-\s\(\)\.]{7,20})?$",
        ErrorMessage = "شماره تلفن معتبر نیست (بین ۷ تا ۲۰ نویسه).")]
    [StringLength(20, ErrorMessage = "تلفن نمی‌تواند بیشتر از ۲۰ کاراکتر باشد.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "آدرس نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Address { get; set; }
}