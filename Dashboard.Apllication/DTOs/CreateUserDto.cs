public class CreateUserDto
{
    public string FullName { get; set; }
    public string PhoneNumber { get; set; } // معمولاً اجباری است
    public string Email { get; set; }
    public string Password { get; set; } // برای ثبت‌نام اولیه
    public string? RoleName { get; set; } // برای تعیین نقش اولیه (اختیاری)
}