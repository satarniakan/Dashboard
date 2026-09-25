using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "نام کامل الزامی است.")]
    [StringLength(100, ErrorMessage = "نام کامل نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است.")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [MinLength(3, ErrorMessage = "رمز عبور باید حداقل ۳ کاراکتر باشد.")]
    public string Password { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "انتخاب حداقل یک نقش الزامی است.")]
    public List<string> RoleNames { get; set; } = new();
}