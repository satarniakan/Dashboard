using Microsoft.AspNetCore.Identity;
using Dashboard.Application.DTOs;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IUserAdminService
{
    Task<IEnumerable<UserListItemDto>> GetAllUsersAsync();
    Task<UserListItemDto?> GetUserAsync(string userId);
    Task<bool> SetRoleAsync(string userId, string roleName);
}

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAdminService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

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
}