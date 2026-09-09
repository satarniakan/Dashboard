using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class JournalEntryRepository : IJournalEntryRepository
{
    private readonly AppDbContext _context;
    public JournalEntryRepository(AppDbContext context) => _context = context;

    public async Task<JournalEntry?> GetByIdAsync(int id) =>
        await _context.JournalEntries
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IEnumerable<JournalEntry>> GetAllAsync() =>
        await _context.JournalEntries
            // ThenInclude(Account) لازم است چون MapToDto در JournalService از l.Account.Code/Name
            // استفاده می‌کند؛ بدونش همیشه "-" برمی‌گشت (به‌خاطر ?? در همان متد، خطا نمی‌داد
            // ولی داده‌ی غلط نشان می‌داد).
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();

    public async Task AddAsync(JournalEntry entry) =>
        await _context.JournalEntries.AddAsync(entry);

    public async Task<IEnumerable<JournalEntryLine>> GetLinesByAccountAsync(int accountId) =>
        await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == accountId)
            .OrderBy(l => l.JournalEntry!.EntryDate)
            .ToListAsync();

    public async Task<IEnumerable<JournalEntryLine>> GetAllLinesAsync() =>
        await _context.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .ToListAsync();
}