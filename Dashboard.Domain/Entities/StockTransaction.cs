using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>
/// دفتر رویداد انبار (append-only ledger). هر تغییر موجودی، دقیقاً یک ردیف اینجا ثبت می‌کند
/// (انتقال بین انبار دو ردیف: یکی TransferOut از انبار مبدا و یکی TransferIn در انبار مقصد).
/// </summary>
public class StockTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public StockTransactionType Type { get; set; }

    /// مثبت = افزایش موجودی، منفی = کاهش موجودی
    public decimal QuantityChange { get; set; }

    /// فقط برای رسید خرید معنادار است (برای محاسبه‌ی میانگین بهای تمام‌شده در آینده)
    public decimal? UnitCost { get; set; }

    /// نام سند مبدا، مثلا "PurchaseReceipt" یا "StockTransfer"
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
