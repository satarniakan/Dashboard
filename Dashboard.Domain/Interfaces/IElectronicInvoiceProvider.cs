namespace Dashboard.Domain.Interfaces;

public record ElectronicInvoiceResult(bool Success, string? TaxUniqueId, string? ErrorMessage);

public interface IElectronicInvoiceProvider
{
    Task<ElectronicInvoiceResult> SubmitInvoiceAsync(int salesInvoiceId);
    Task<string> InquiryStatusAsync(string taxUniqueId);
}