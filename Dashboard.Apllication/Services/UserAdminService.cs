using Microsoft.AspNetCore.Identity;
using Dashboard.Application.DTOs;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IUserAdminService
{
    // متدهای قبلی شما
    Task<IEnumerable<UserListItemDto>> GetAllUsersAsync();
    Task<UserListItemDto?> GetUserAsync(string userId);
    Task<bool> SetRoleAsync(string userId, string roleName);

    // ===== متدهای جدید برای مدیریت کاربر =====
    Task<IdentityResult> CreateUserAsync(CreateUserDto model);
    Task<List<RoleDto>> GetAllRolesAsync(); // متد جدید برای دریافت نقش‌ها
}

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager; // اضافه کنید

    // سازنده را به‌روز کنید
    public UserAdminService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // متدهای قبلی شما بدون تغییر
    public async Task<IEnumerable<UserListItemDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users.ToList();
        var result = new List<UserListItemDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserListItemDto(
                user.Id, user.PhoneNumber, user.FullName, user.Email, roles.FirstOrDefault()));
        }

        return result;
    }

    public async Task<UserListItemDto?> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserListItemDto(user.Id, user.PhoneNumber, user.FullName, user.Email, roles.FirstOrDefault());
    }

    public async Task<bool> SetRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);
        return result.Succeeded;
    }

    // ===== متدهای جدید =====

    // متد برای ایجاد کاربر جدید
    public async Task<IdentityResult> CreateUserAsync(CreateUserDto model)
    {
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingByEmail is not null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "این ایمیل قبلاً استفاده شده است." });
            }
        }
        var user = new ApplicationUser
        {
            UserName = model.PhoneNumber, // شماره موبایل به عنوان نام کاربری
            PhoneNumber = model.PhoneNumber,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true // برای سادگی، ایمیل را تأیید شده در نظر می‌گیریم
        };

        // ایجاد کاربر
        var result = await _userManager.CreateAsync(user, model.Password);

        // اگر کاربر ساخته شد و نقش انتخاب شده بود، نقش را اعمال کن
        if (result.Succeeded && !string.IsNullOrEmpty(model.RoleName))
        {
            var roleExists = await _roleManager.RoleExistsAsync(model.RoleName);
            if (roleExists)
            {
                await _userManager.AddToRoleAsync(user, model.RoleName);
            }
            else
            {
                // اگر نقش وجود نداشت، کاربر را حذف کن تا داده‌ها ناقص نمانند
                await _userManager.DeleteAsync(user);
                return IdentityResult.Failed(new IdentityError { Description = "نقش انتخاب شده معتبر نیست" });
            }
        }

        return result;
    }

    // متد برای دریافت لیست نقش‌ها
    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = _roleManager.Roles.ToList(); // گرفتن لیست نقش‌ها از دیتابیس
        return roles.Select(r => new RoleDto
        {
            Id = r.Id ?? string.Empty,
            Name = r.Name ?? string.Empty,
            PersianName = Dashboard.Domain.Identity.Roles.ToPersian(r.Name ?? string.Empty)
        }).ToList();
    }
}