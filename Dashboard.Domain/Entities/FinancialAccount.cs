using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>صندوق نقدی یا حساب بانکی — به یک سرفصل حساب در دفتر کل وصل است</summary>
public class FinancialAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FinancialAccountType Type { get; set; }

    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Iban { get; set; }

    // سرفصل معادل این صندوق/بانک در دفتر کل
    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public bool IsActive { get; set; } = true;
}