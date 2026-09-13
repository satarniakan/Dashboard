using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IInternalIssueRepository
{
    Task<InternalIssue?> GetByIdAsync(int id);
    Task<IEnumerable<InternalIssue>> GetAllAsync();
    Task AddAsync(InternalIssue issue);
    Task UpdateAsync(InternalIssue issue);
}
