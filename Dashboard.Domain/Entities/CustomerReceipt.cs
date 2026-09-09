using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>دریافت وجه از مشتری (بابت فاکتور یا کلی)</summary>
public class CustomerReceipt
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // اگر این دریافت بابت یک قسط مشخص است (اختیاری)
    public int? InstallmentId { get; set; }
    public Installment? Installment { get; set; }

}