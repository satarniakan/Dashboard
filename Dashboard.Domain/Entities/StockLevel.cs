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
    /// مقدارِ رزروشده برای سفارش‌های فروشگاه که هنوز پرداخت نشده‌اند.
    /// «موجودی قابل فروش = QuantityOnHand − ReservedQuantity»، پس کالای رزروشده را
    /// کاربر/سفارش دیگری نمی‌تواند بخرد و فروش بیش از موجودی رخ نمی‌دهد.
    /// با پرداخت موفق یا لغو/انقضای سفارش، رزرو آزاد می‌شود.
    /// </summary>
    public decimal ReservedQuantity { get; set; }

    /// <summary>
    /// توکن همزمانی خوش‌بینانه (Optimistic Concurrency) — برای جلوگیری از race condition
    /// در کسر/افزایش موجودی هنگام درخواست‌های همزمان
    /// </summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
