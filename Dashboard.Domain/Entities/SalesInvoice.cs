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

    /// <summary>هزینهٔ حمل‌ونقل — از سفارش فروشگاه می‌آید تا مبلغ فاکتور دقیقاً برابر مبلغ پرداختی مشتری باشد</summary>
    public decimal ShippingAmount { get; set; }

    /// <summary>درصد مالیات بر ارزش افزوده در لحظهٔ صدور — عکس فوری؛ صفر یعنی بدون مالیات</summary>
    public decimal TaxPercent { get; set; }

    /// <summary>مبلغ مالیات بر ارزش افزوده — جزء TotalAmount است اما درآمد نیست؛ در ۲۳۰۰ بستانکار می‌شود</summary>
    public decimal TaxAmount { get; set; }

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

    /// <summary>
    /// بهای تمام‌شدهٔ کالا در لحظهٔ صدور فاکتور — عکس فوری.
    /// سند فروش و سندِ برگشتِ همین فاکتور باید با همین عدد ببندند، وگرنه
    /// تغییر بعدیِ قیمت تمام‌شدهٔ کالا موجودی و سود ناخالص را نامیزان می‌کند.
    /// null یعنی فاکتور قدیمی است که این مقدار را ندارد (محاسبه از قیمت روز کالا).
    /// </summary>
    public decimal? CostPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}