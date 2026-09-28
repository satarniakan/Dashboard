using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record FinancialAccountDto(int Id, string Name, string Type);

public class CreateFinancialAccountDto
{
    [Required(ErrorMessage = "نام صندوق/بانک الزامی است.")]
    [StringLength(150, ErrorMessage = "نام صندوق/بانک نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع حساب الزامی است.")]
    [AllowedValues("Cash", "Bank", ErrorMessage = "نوع حساب باید «صندوق نقدی» یا «حساب بانکی» باشد.")]
    public string Type { get; set; } = "Cash"; // "Cash" یا "Bank"

    [StringLength(150, ErrorMessage = "نام بانک نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string? BankName { get; set; }

    [StringLength(50, ErrorMessage = "شماره حساب نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? AccountNumber { get; set; }

    [RegularExpression(@"^(?:IR)?[0-9]{2,34}$", ErrorMessage = "شماره شبا معتبر نیست.")]
    [StringLength(34, ErrorMessage = "شماره شبا نمی‌تواند بیشتر از ۳۴ کاراکتر باشد.")]
    public string? Iban { get; set; }

}

public class CreateCustomerReceiptDto
{
    public int? CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب صندوق/بانک الزامی است.")]
    public int FinancialAccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ باید بزرگتر از صفر باشد.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "روش دریافت الزامی است.")]
    [AllowedValues("Cash", "CardTransfer", "Cheque", "BankTransfer",
        ErrorMessage = "روش دریافت نامعتبر است.")]
    public string Method { get; set; } = "Cash";

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    [StringLength(50, ErrorMessage = "شماره چک نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? ChequeNumber { get; set; }

    public DateTime? ChequeDueDate { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    public int? InstallmentId { get; set; }
}

public record CustomerReceiptDto(int Id, string ReceiptNumber, DateTime ReceiptDate, string? CustomerName, string FinancialAccountName, decimal Amount, string Method);

public class CreateSupplierPaymentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب تأمین‌کننده الزامی است.")]
    public int SupplierId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب صندوق/بانک الزامی است.")]
    public int FinancialAccountId { get; set; }

    [StringLength(50, ErrorMessage = "شماره پرداخت نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string PaymentNumber { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ باید بزرگتر از صفر باشد.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "روش پرداخت الزامی است.")]
    [AllowedValues("Cash", "CardTransfer", "Cheque", "BankTransfer",
        ErrorMessage = "روش پرداخت نامعتبر است.")]
    public string Method { get; set; } = "Cash";

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [StringLength(50, ErrorMessage = "شماره چک نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? ChequeNumber { get; set; }

    public DateTime? ChequeDueDate { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }
}

public record SupplierPaymentDto(int Id, string PaymentNumber, DateTime PaymentDate, string SupplierName, string FinancialAccountName, decimal Amount, string Method);

/// <summary>چکِ در جریان (Pending) برای صفحهٔ «تسویه اوراق».</summary>
public record PendingChequeDto(
    int Id,                 // Id رسید دریافت یا سند پرداخت، بسته به Kind
    string Kind,            // "receipt" (چک دریافتی) یا "payment" (چک پرداختی)
    string DocumentNumber,  // شماره رسید / سند پرداخت
    string? Counterparty,   // نام مشتری / تأمین‌کننده
    string? ChequeNumber,
    DateTime? ChequeDueDate,
    decimal Amount);

/// <summary>وصول چک: پول به این صندوق/بانک می‌رود.</summary>
public class SettleChequeDto
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب صندوق/بانک مقصد الزامی است.")]
    public int FinancialAccountId { get; set; }

    public DateTime? SettledAt { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }
}

/// <summary>برگشت‌خوردن چک: بدهی/طلب به حالت قبل برمی‌گردد.</summary>
public class BounceChequeDto
{
    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }
}

public class TransferBetweenAccountsDto
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب صندوق/بانک مبدأ الزامی است.")]
    public int FromFinancialAccountId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب صندوق/بانک مقصد الزامی است.")]
    public int ToFinancialAccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ باید بزرگتر از صفر باشد.")]
    public decimal Amount { get; set; }

    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }
}
