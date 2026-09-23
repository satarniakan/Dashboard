using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IAuditService
{
    Task LogEventAsync(string eventType, string? userEmail, string details);
    Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100);
    Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    public AuditService(IAuditLogRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task LogEventAsync(string eventType, string? userEmail, string details)
    {
        await _repository.AddAsync(new AuditLog
        {
            EventType = eventType,
            UserEmail = userEmail,
            Details = details,
            OccurredAt = DateTime.UtcNow
        });

        await _unitOfWork.CompleteAsync();
    }
    public async Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100) =>
        await _repository.GetRecentAsync(count);
    public async Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, search);

        return new PagedResult<AuditLog>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}