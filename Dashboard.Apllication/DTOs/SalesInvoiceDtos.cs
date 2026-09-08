namespace Dashboard.Application.DTOs;

public class SalesInvoiceItemInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreateSalesInvoiceDto
{
    public int? CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<SalesInvoiceItemInput> Items { get; set; } = new();
}

public record SalesInvoiceItemDto(int ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record SalesInvoiceDto(
    int Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string? CustomerName,
    string WarehouseName,
    string Status,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? Notes,
    List<SalesInvoiceItemDto> Items);

public record SalesInvoiceSummaryDto(
    int Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string? CustomerName,
    string WarehouseName,
    string Status,
    decimal TotalAmount);