namespace Dashboard.Domain.Entities;

/// <summary>رسید خرید از تأمین‌کننده — سرِ سند</summary>
public class PurchaseReceipt
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// مبلغ مالیات بر ارزش افزودهٔ خرید (اعتبار مالیاتی) — به ۱۳۵۰ بدهکار می‌شود و
    /// بهای تمام‌شدهٔ کالا را تغییر نمی‌دهد؛ UnitCost سطرها «خالص» در نظر گرفته می‌شود.
    /// </summary>
    public decimal TaxAmount { get; set; }

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PurchaseReceiptItem> Items { get; set; } = new();
}

public class PurchaseReceiptItem
{
    public int Id { get; set; }
    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
