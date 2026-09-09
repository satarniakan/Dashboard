namespace Dashboard.Application.DTOs;

public record InstallmentInput(DateTime DueDate, decimal Amount);

public class CreateInstallmentPlanDto
{
    public int SalesInvoiceId { get; set; }
    public List<InstallmentInput> Installments { get; set; } = new();
}

public record InstallmentDto(int Id, int SequenceNumber, DateTime DueDate, decimal Amount, decimal PaidAmount, bool IsFullyPaid, bool IsOverdue);

public record InstallmentPlanDto(int Id, int SalesInvoiceId, string InvoiceNumber, string? CustomerName, decimal TotalAmount, List<InstallmentDto> Installments);

public record OverdueInstallmentDto(int InstallmentId, string InvoiceNumber, string CustomerName, DateTime DueDate, decimal Amount, decimal PaidAmount, int DaysOverdue);