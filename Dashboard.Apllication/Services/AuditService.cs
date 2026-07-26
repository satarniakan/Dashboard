// Dashboard.Application/Services/AuditService.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IAuditService
{
    Task LogEventAsync(string eventType, string? userEmail, string details);
    Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;

    public AuditService(IAuditLogRepository repository) => _repository = repository;

    public async Task LogEventAsync(string eventType, string? userEmail, string details)
    {
        await _repository.AddAsync(new AuditLog
        {
            EventType = eventType,
            UserEmail = userEmail,
            Details = details,
            OccurredAt = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100) =>
        await _repository.GetRecentAsync(count);
}