using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>فاکتور فروش — سرِ سند</summary>
public class SalesInvoice
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    public List<SalesInvoiceItem> Items { get; set; } = new();
}

public class SalesInvoiceItem
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }

    // قیمت لحظه فروش — عکس فوری، نه ارجاع زنده به قیمت محصول
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}