using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IJournalService
{
    Task<int> PostEntryAsync(
        string description,
        List<JournalLineInput> lines,
        string? referenceType = null,
        int? referenceId = null,
        string? userId = null);

    Task<IEnumerable<JournalEntryDto>> GetEntriesAsync();
    Task<JournalEntryDto?> GetEntryAsync(int id);
    Task<IEnumerable<TrialBalanceRowDto>> GetTrialBalanceAsync();
    Task<List<AccountStatementRowDto>> GetCustomerStatementAsync(int customerId);
    Task<List<AccountStatementRowDto>> GetSupplierStatementAsync(int supplierId);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from = null, DateTime? to = null);
}

public class JournalService : IJournalService
{
    private readonly IUnitOfWork _unitOfWork;

    public JournalService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> PostEntryAsync(
        string description,
        List<JournalLineInput> lines,
        string? referenceType = null,
        int? referenceId = null,
        string? userId = null)
    {
        if (lines.Count < 2)
            throw new InvalidOperationException("سند حسابداری باید حداقل دو سطر داشته باشد.");

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        // قانون طلایی حسابداری دوطرفه — اگر این‌جا نگه ندارید، هیچ گزارش مالی قابل اعتماد نخواهد بود
        if (totalDebit != totalCredit)
            throw new InvalidOperationException(
                $"سند نامتوازن است: جمع بدهکار ({totalDebit}) با جمع بستانکار ({totalCredit}) برابر نیست.");

        var entry = new JournalEntry
        {
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsSystemGenerated = true,
            CreatedByUserId = userId
        };

        foreach (var line in lines)
        {
            var account = await _unitOfWork.Accounts.GetByCodeAsync(line.AccountCode)
                ?? throw new InvalidOperationException($"حساب با کد '{line.AccountCode}' یافت نشد.");

            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = account.Id,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                Description = line.Description,
                SubsidiaryType = line.SubsidiaryType,
                SubsidiaryId = line.SubsidiaryId
            });
        }

        await _unitOfWork.JournalEntries.AddAsync(entry);
        await _unitOfWork.CompleteAsync();

        return entry.Id;
    }
    public async Task<IEnumerable<JournalEntryDto>> GetEntriesAsync()
    {
        var entries = await _unitOfWork.JournalEntries.GetAllAsync();
        return entries.Select(MapToDto);
    }

    public async Task<JournalEntryDto?> GetEntryAsync(int id)
    {
        var entry = await _unitOfWork.JournalEntries.GetByIdAsync(id);
        return entry is null ? null : MapToDto(entry);
    }

    public async Task<IEnumerable<TrialBalanceRowDto>> GetTrialBalanceAsync()
    {
        var lines = await _unitOfWork.JournalEntries.GetAllLinesAsync();

        return lines
            .GroupBy(l => l.Account!)
            .Select(g => new TrialBalanceRowDto(
                g.Key.Code,
                g.Key.Name,
                g.Key.Type.ToString(),
                g.Sum(l => l.DebitAmount),
                g.Sum(l => l.CreditAmount),
                g.Sum(l => l.DebitAmount) - g.Sum(l => l.CreditAmount)))
            .OrderBy(r => r.AccountCode)
            .ToList();
    }

    private static JournalEntryDto MapToDto(Domain.Entities.JournalEntry entry) =>
        new(entry.Id, entry.EntryNumber, entry.EntryDate, entry.Description,
            entry.ReferenceType, entry.ReferenceId,
            entry.Lines.Select(l => new JournalEntryLineDto(
                l.Account?.Code ?? "-", l.Account?.Name ?? "-", l.DebitAmount, l.CreditAmount, l.Description)).ToList());
    public async Task<List<AccountStatementRowDto>> GetCustomerStatementAsync(int customerId) =>
    await BuildSubsidiaryStatementAsync("Customer", customerId);

    public async Task<List<AccountStatementRowDto>> GetSupplierStatementAsync(int supplierId) =>
        await BuildSubsidiaryStatementAsync("Supplier", supplierId);

    private async Task<List<AccountStatementRowDto>> BuildSubsidiaryStatementAsync(string subsidiaryType, int subsidiaryId)
    {
        var allLines = await _unitOfWork.JournalEntries.GetAllLinesAsync();

        var relevantLines = allLines
            .Where(l => l.SubsidiaryType == subsidiaryType && l.SubsidiaryId == subsidiaryId)
            .OrderBy(l => l.JournalEntry!.EntryDate)
            .ToList();

        var result = new List<AccountStatementRowDto>();
        decimal runningBalance = 0;

        foreach (var line in relevantLines)
        {
            runningBalance += line.DebitAmount - line.CreditAmount;
            result.Add(new AccountStatementRowDto(
                line.JournalEntry!.EntryDate,
                line.JournalEntry.EntryNumber,
                line.Description ?? line.JournalEntry.Description,
                line.DebitAmount,
                line.CreditAmount,
                runningBalance));
        }

        return result;
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from = null, DateTime? to = null)
    {
        var allLines = await _unitOfWork.JournalEntries.GetAllLinesAsync();

        var filtered = allLines.Where(l =>
            (!from.HasValue || l.JournalEntry!.EntryDate >= from.Value) &&
            (!to.HasValue || l.JournalEntry!.EntryDate <= to.Value));

        var revenueLines = filtered.Where(l => l.Account!.Type == Domain.Enums.AccountType.Revenue).ToList();
        var expenseLines = filtered.Where(l => l.Account!.Type == Domain.Enums.AccountType.Expense).ToList();

        var revenueBreakdown = revenueLines
            .GroupBy(l => l.Account!.Name)
            .Select(g => (g.Key, g.Sum(l => l.CreditAmount - l.DebitAmount)))
            .ToList();

        var expenseBreakdown = expenseLines
            .GroupBy(l => l.Account!.Name)
            .Select(g => (g.Key, g.Sum(l => l.DebitAmount - l.CreditAmount)))
            .ToList();

        var totalRevenue = revenueBreakdown.Sum(r => r.Item2);
        var totalExpense = expenseBreakdown.Sum(e => e.Item2);

        return new ProfitAndLossDto(totalRevenue, totalExpense, totalRevenue - totalExpense, revenueBreakdown, expenseBreakdown);
    }
}