using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>پرداخت وجه به تأمین‌کننده</summary>
public class SupplierPayment
{
    public int Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}