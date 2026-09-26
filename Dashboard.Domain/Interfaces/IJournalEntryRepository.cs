using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Interfaces;

/// <summary>نتیجه‌ی تجمیع SQL برای تراز آزمایشی — یک سطر به ازای هر حساب</summary>
public record TrialBalanceRow(string AccountCode, string AccountName, AccountType AccountType, decimal TotalDebit, decimal TotalCredit);

/// <summary>سطر گردش حساب معین (مشتری/فروشنده) با مشخصات سند — فیلتر و مرتب‌سازی در SQL انجام می‌شود</summary>
public record SubsidiaryLineRow(DateTime EntryDate, string EntryNumber, string? LineDescription, string EntryDescription, decimal DebitAmount, decimal CreditAmount);

/// <summary>جمع بدهکار/بستانکار به تفکیک نام حساب، فقط برای حساب‌های درآمد/هزینه در بازه‌ی زمانی</summary>
public record AccountTypeSumRow(AccountType AccountType, string AccountName, decimal TotalDebit, decimal TotalCredit);

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(int id);
    Task<IEnumerable<JournalEntry>> GetAllAsync();
    Task AddAsync(JournalEntry entry);

    // گزارش‌های مالی: تجمیع مستقیم در دیتابیس انجام می‌شود تا کل سطرها به حافظه بارگذاری نشود
    Task<IEnumerable<TrialBalanceRow>> GetTrialBalanceRowsAsync();
    Task<IEnumerable<SubsidiaryLineRow>> GetSubsidiaryLinesAsync(string subsidiaryType, int subsidiaryId);
    Task<IEnumerable<AccountTypeSumRow>> GetRevenueExpenseSumsAsync(DateTime? from, DateTime? to);
    Task<decimal> GetAccountNetBalanceAsync(string accountCode);
}
