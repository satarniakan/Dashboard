namespace Dashboard.Application.DTOs;

public record FinancialAccountDto(int Id, string Name, string Type);

public class CreateFinancialAccountDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Cash"; // "Cash" یا "Bank"
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Iban { get; set; }

}

public class CreateCustomerReceiptDto
{
    public int? CustomerId { get; set; }
    public int FinancialAccountId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public string? Notes { get; set; }
    public int? InstallmentId { get; set; }
}

public record CustomerReceiptDto(int Id, string ReceiptNumber, DateTime ReceiptDate, string? CustomerName, string FinancialAccountName, decimal Amount, string Method);

public class CreateSupplierPaymentDto
{
    public int SupplierId { get; set; }
    public int FinancialAccountId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public string? Notes { get; set; }
}

public record SupplierPaymentDto(int Id, string PaymentNumber, DateTime PaymentDate, string SupplierName, string FinancialAccountName, decimal Amount, string Method);