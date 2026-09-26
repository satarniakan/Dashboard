// Dashboard.Infrastructure/Repositories/NotificationRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context) => _context = context;

    public async Task<Notification?> GetByIdAsync(int id) =>
        await _context.Notifications.FindAsync(id);

    public async Task<List<Notification>> GetForUserAsync(string userId, int take = 20, bool? onlyUnread = null)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (onlyUnread == true)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task AddAsync(Notification notification) =>
        await _context.Notifications.AddAsync(notification);

    public Task UpdateAsync(Notification notification)
    {
        _context.Notifications.Update(notification);
        return Task.CompletedTask;
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task<List<string>> GetAllUserIdsAsync() =>
        await _context.Users.Select(u => u.Id).ToListAsync();

    public async Task<List<string>> GetUserIdsInRoleAsync(string roleName) =>
        await (from userRole in _context.UserRoles
               join role in _context.Roles on userRole.RoleId equals role.Id
               join user in _context.Users on userRole.UserId equals user.Id
               where role.Name == roleName
               select user.Id)
            .Distinct()
            .ToListAsync();
}
