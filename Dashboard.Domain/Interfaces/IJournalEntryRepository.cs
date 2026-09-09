using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(int id);
    Task<IEnumerable<JournalEntry>> GetAllAsync();
    Task AddAsync(JournalEntry entry);

    // برای گزارش گردش حساب و تراز آزمایشی، مستقیم روی سطرها کوئری می‌زنیم
    Task<IEnumerable<JournalEntryLine>> GetLinesByAccountAsync(int accountId);
    Task<IEnumerable<JournalEntryLine>> GetAllLinesAsync();

    
}