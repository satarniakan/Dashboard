using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class ScrapRecordRepository : IScrapRecordRepository
{
    private readonly AppDbContext _context;
    public ScrapRecordRepository(AppDbContext context) => _context = context;

    public async Task<ScrapRecord?> GetByIdAsync(int id) =>
        await _context.ScrapRecords
            .Include(r => r.Items).ThenInclude(x => x.Product)
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<ScrapRecord>> GetAllAsync() =>
        await _context.ScrapRecords
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.RecordDate)
            .ToListAsync();

    public async Task AddAsync(ScrapRecord scrapRecord) =>
        await _context.ScrapRecords.AddAsync(scrapRecord);
}
