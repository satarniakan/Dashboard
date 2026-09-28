namespace Dashboard.Domain.Entities;

/// <summary>
/// برگشت خرید به تأمین‌کننده — سرِ سند. همیشه به یک رسید خرید مرجع گره خورده است:
/// بدون آن نه سهم پرداختنیِ قابل‌بدهکارشدن مشخص است و نه بهای تمام‌شدهٔ اصلیِ اقلام
/// (برخلاف بهای میانگینِ فعلی) — و سند حسابداری برگشت همین را لازم دارد.
/// </summary>
public class PurchaseReturn
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public string? SupplierReference { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }
    public List<PurchaseReturnItem> Items { get; set; } = new();
}

public class PurchaseReturnItem
{
    public int Id { get; set; }
    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
}
