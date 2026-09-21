namespace Dashboard.Domain.Entities;

/// <summary>
/// موجودی جاری هر کالا در هر انبار. این جدول یک کَش/خلاصه از جمع StockTransaction هاست
/// و هیچ‌وقت نباید مستقیم و بدون ثبت یک StockTransaction تغییر کند.
/// </summary>
public class StockLevel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// توکن همزمانی خوش‌بینانه (Optimistic Concurrency) — برای جلوگیری از race condition
    /// در کسر/افزایش موجودی هنگام درخواست‌های همزمان
    /// </summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
