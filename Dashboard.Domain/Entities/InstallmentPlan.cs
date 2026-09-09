namespace Dashboard.Domain.Entities;

/// <summary>طرح اقساط برای یک فاکتور فروش تأییدشده</summary>
public class InstallmentPlan
{
    public int Id { get; set; }

    public int SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }

    public List<Installment> Installments { get; set; } = new();
}

public class Installment
{
    public int Id { get; set; }

    public int InstallmentPlanId { get; set; }
    public InstallmentPlan? InstallmentPlan { get; set; }

    public int SequenceNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }

    public bool IsFullyPaid => PaidAmount >= Amount;
    public bool IsOverdue => !IsFullyPaid && DueDate.Date < DateTime.UtcNow.Date;
}