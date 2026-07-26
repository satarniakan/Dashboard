// Dashboard.Domain/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Dashboard.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}