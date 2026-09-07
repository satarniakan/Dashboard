namespace Dashboard.Domain.Entities;

public class SalesInvoiceLine
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    // قیمت لحظه فروش — عکس فوری، نه ارجاع به قیمت زنده محصول
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}