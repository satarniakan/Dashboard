namespace Dashboard.Application.DTOs;

public record JournalLineInput(
    string AccountCode, decimal Debit, decimal Credit, string? Description = null,
    string? SubsidiaryType = null, int? SubsidiaryId = null);
public record JournalEntryLineDto(string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Description);

public record JournalEntryDto(
    int Id, string EntryNumber, DateTime EntryDate, string Description,
    string? ReferenceType, int? ReferenceId, List<JournalEntryLineDto> Lines);

public record TrialBalanceRowDto(string AccountCode, string AccountName, string AccountType, decimal TotalDebit, decimal TotalCredit, decimal Balance);
public record AccountStatementRowDto(DateTime Date, string EntryNumber, string Description, decimal Debit, decimal Credit, decimal RunningBalance);

public record ProfitAndLossDto(
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal NetProfit,
    List<(string AccountName, decimal Amount)> RevenueBreakdown,
    List<(string AccountName, decimal Amount)> ExpenseBreakdown);