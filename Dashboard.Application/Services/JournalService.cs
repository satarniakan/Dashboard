using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
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
            throw new BusinessRuleException("سند حسابداری باید حداقل دو سطر داشته باشد.");

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        // مبلغ منفی در سند یعنی همان مبلغ با علامت معکوس در سمت دیگر ثبت می‌شود؛
        // این دفتر کل را در ترازِ عددی نگه می‌دارد ولی گزارش سود و زیان و مانده‌ی
        // حساب را بی‌معنا می‌کند. «برگشت» با سندِ معکوسِ جدا انجام می‌شود، نه با عدد منفی.
        if (lines.Any(l => l.Debit < 0 || l.Credit < 0))
            throw new BusinessRuleException("مبلغ سطر سند نمی‌تواند منفی باشد؛ برای برگشت، سند معکوس ثبت کنید.");

        // هر سطر باید یا بدهکار باشد یا بستانکار — نه هر دو و نه هیچ‌کدام.
        foreach (var line in lines)
        {
            if (line.Debit > 0 && line.Credit > 0)
                throw new BusinessRuleException(
                    $"سطر سند نمی‌تواند هم‌زمان بدهکار و بستانکار باشد (حساب {line.AccountCode}).");
            if (line.Debit == 0 && line.Credit == 0)
                throw new BusinessRuleException(
                    $"سطر سند نمی‌تواند هم بدهکار و هم بستانکار صفر باشد (حساب {line.AccountCode}).");
        }

        // قانون طلایی حسابداری دوطرفه — اگر این‌جا نگه ندارید، هیچ گزارش مالی قابل اعتماد نخواهد بود
        if (totalDebit != totalCredit)
            throw new BusinessRuleException(
                $"سند نامتوازن است: جمع بدهکار ({totalDebit}) با جمع بستانکار ({totalCredit}) برابر نیست.");

        var entry = new JournalEntry
        {
            // پسوند تصادفی: دو سند همزمان در یک ثانیه شماره‌ی تکراری نمی‌گیرند (ایندکس یکتا نقض نمی‌شود)
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsSystemGenerated = true,
            CreatedByUserId = userId
        };

        foreach (var line in lines)
        {
            var account = await _unitOfWork.Accounts.GetByCodeAsync(line.AccountCode)
                ?? throw new NotFoundException("حساب", line.AccountCode);

            // حساب غیرفعال نباید سطر جدید بگیرد؛ اگر گرفته شود تراز آزمایشی سرفصلی را
            // نشان می‌دهد که در UI «بسته» شده و گردشش گم می‌شود.
            if (!account.IsActive)
                throw new BusinessRuleException($"حساب {line.AccountCode} غیرفعال است؛ نمی‌توان روی آن سطر سند زد.");

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
        var rows = await _unitOfWork.JournalEntries.GetTrialBalanceRowsAsync();

        return rows
            .Select(r => new TrialBalanceRowDto(
                r.AccountCode,
                r.AccountName,
                r.AccountType.ToString(),
                r.TotalDebit,
                r.TotalCredit,
                r.TotalDebit - r.TotalCredit))
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
        var lines = await _unitOfWork.JournalEntries.GetSubsidiaryLinesAsync(subsidiaryType, subsidiaryId);

        var result = new List<AccountStatementRowDto>();
        decimal runningBalance = 0;

        foreach (var line in lines)
        {
            runningBalance += line.DebitAmount - line.CreditAmount;
            result.Add(new AccountStatementRowDto(
                line.EntryDate,
                line.EntryNumber,
                line.LineDescription ?? line.EntryDescription,
                line.DebitAmount,
                line.CreditAmount,
                runningBalance));
        }

        return result;
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from = null, DateTime? to = null)
    {
        // «تا تاریخ» یک روزِ تقویمی است (پیکر ابتدای روز می‌دهد) و باید *شامل* آن روز باشد،
        // پس مرز بالا به ابتدای روز بعد تبدیل و به‌صورت exclusive اعمال می‌شود.
        var toExclusive = to?.Date.AddDays(1);
        var sums = (await _unitOfWork.JournalEntries.GetRevenueExpenseSumsAsync(from?.Date, toExclusive)).ToList();

        var revenueBreakdown = sums
            .Where(s => s.AccountType == Domain.Enums.AccountType.Revenue)
            .Select(s => (AccountName: s.AccountName, Amount: s.TotalCredit - s.TotalDebit))
            .ToList();

        var expenseBreakdown = sums
            .Where(s => s.AccountType == Domain.Enums.AccountType.Expense)
            .Select(s => (AccountName: s.AccountName, Amount: s.TotalDebit - s.TotalCredit))
            .ToList();

        var totalRevenue = revenueBreakdown.Sum(r => r.Amount);
        var totalExpense = expenseBreakdown.Sum(e => e.Amount);

        return new ProfitAndLossDto(totalRevenue, totalExpense, totalRevenue - totalExpense, revenueBreakdown, expenseBreakdown);
    }
}