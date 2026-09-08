using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IScrapRecordRepository
{
    Task<ScrapRecord?> GetByIdAsync(int id);
    Task<IEnumerable<ScrapRecord>> GetAllAsync();
    Task AddAsync(ScrapRecord scrapRecord);
}
