using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
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

    public async Task<IEnumerable<TrialBalanceRow>> GetTrialBalanceRowsAsync() =>
        await _context.JournalEntryLines
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.Type })
            .OrderBy(g => g.Key.Code)
            .Select(g => new TrialBalanceRow(g.Key.Code, g.Key.Name, g.Key.Type,
                g.Sum(l => l.DebitAmount), g.Sum(l => l.CreditAmount)))
            .ToListAsync();

    public async Task<IEnumerable<SubsidiaryLineRow>> GetSubsidiaryLinesAsync(string subsidiaryType, int subsidiaryId) =>
        await _context.JournalEntryLines
            .Where(l => l.SubsidiaryType == subsidiaryType && l.SubsidiaryId == subsidiaryId)
            .OrderBy(l => l.JournalEntry!.EntryDate).ThenBy(l => l.JournalEntry!.Id).ThenBy(l => l.Id)
            .Select(l => new SubsidiaryLineRow(
                l.JournalEntry!.EntryDate, l.JournalEntry!.EntryNumber,
                l.Description, l.JournalEntry!.Description,
                l.DebitAmount, l.CreditAmount))
            .ToListAsync();

    public async Task<IEnumerable<AccountTypeSumRow>> GetRevenueExpenseSumsAsync(DateTime? from, DateTime? to) =>
        await _context.JournalEntryLines
            .Where(l => (l.Account!.Type == AccountType.Revenue || l.Account!.Type == AccountType.Expense)
                        && (from == null || l.JournalEntry!.EntryDate >= from)
                        && (to == null || l.JournalEntry!.EntryDate <= to))
            .GroupBy(l => new { l.Account!.Type, l.Account.Name })
            .Select(g => new AccountTypeSumRow(g.Key.Type, g.Key.Name,
                g.Sum(l => l.DebitAmount), g.Sum(l => l.CreditAmount)))
            .ToListAsync();

    public async Task<decimal> GetAccountNetBalanceAsync(string accountCode) =>
        await _context.JournalEntryLines
            .Where(l => l.Account!.Code == accountCode)
            .SumAsync(l => (decimal?)(l.DebitAmount - l.CreditAmount)) ?? 0m;
}
