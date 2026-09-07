using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

public class SalesInvoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }

    public List<SalesInvoiceLine> Lines { get; set; } = new();
}