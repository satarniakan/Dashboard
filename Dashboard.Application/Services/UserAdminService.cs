using Microsoft.AspNetCore.Identity;
using Dashboard.Application.DTOs;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IUserAdminService
{
    Task<IEnumerable<UserListItemDto>> GetAllUsersAsync();
    Task<UserListItemDto?> GetUserAsync(string userId);
    Task<IdentityResult> SetRolesAsync(string userId, List<string> roleNames);

    Task<IdentityResult> CreateUserAsync(CreateUserDto model);
    Task<List<RoleDto>> GetAllRolesAsync();
}

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserAdminService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IEnumerable<UserListItemDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users.ToList();
        var result = new List<UserListItemDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserListItemDto(
                user.Id, user.PhoneNumber, user.FullName, user.Email, roles.ToList()));
        }

        return result;
    }

    public async Task<UserListItemDto?> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserListItemDto(user.Id, user.PhoneNumber, user.FullName, user.Email, roles.ToList());
    }

    public async Task<IdentityResult> SetRolesAsync(string userId, List<string> roleNames)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Description = "کاربر یافت نشد." });

        // اعتبارسنجی وجود نقش‌ها پیش از هر تغییری: در غیر این صورت AddToRolesAsync شکست
        // می‌خورد ولی قبلاً بی‌صدا نادیده گرفته می‌شد و UI «موفق» نشان می‌داد.
        var unknown = new List<string>();
        foreach (var roleName in roleNames)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                unknown.Add(roleName);
        }
        if (unknown.Any())
            return IdentityResult.Failed(new IdentityError { Description = $"این نقش‌ها وجود ندارند: {string.Join("، ", unknown)}" });

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return removeResult;
        }

        if (roleNames.Any())
        {
            var addResult = await _userManager.AddToRolesAsync(user, roleNames);
            if (!addResult.Succeeded) return addResult;
        }

        return IdentityResult.Success;
    }

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
            UserName = model.PhoneNumber,
            PhoneNumber = model.PhoneNumber,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded && model.RoleNames is { Count: > 0 })
        {
            var validRoles = new List<string>();
            foreach (var roleName in model.RoleNames)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    validRoles.Add(roleName);
                }
            }

            if (validRoles.Any())
            {
                await _userManager.AddToRolesAsync(user, validRoles);
            }
        }

        return result;
    }

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = _roleManager.Roles.ToList();
        return roles.Select(r => new RoleDto
        {
            Id = r.Id ?? string.Empty,
            Name = r.Name ?? string.Empty,
            PersianName = Dashboard.Domain.Identity.Roles.ToPersian(r.Name ?? string.Empty)
        }).ToList();
    }
}