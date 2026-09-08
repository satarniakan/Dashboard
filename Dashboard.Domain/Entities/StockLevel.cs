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
}
