// Dashboard.Domain/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Dashboard.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    // برای نمایش سریع تو لیست‌ها (خودکار از نام + نام‌خانوادگی ساخته می‌شود)
    public string? FullName { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
}