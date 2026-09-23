// Dashboard.Domain/Interfaces/IAuditLogRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
}