using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>انبارگردانی — سرِ سند</summary>
public class StockCount
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public StockCountStatus Status { get; set; } = StockCountStatus.Open;
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public List<StockCountItem> Items { get; set; } = new();
}

public class StockCountItem
{
    public int Id { get; set; }
    public int StockCountId { get; set; }
    public StockCount? StockCount { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// موجودی سیستمی در لحظه‌ی شروع شمارش (اسنپ‌شات)
    public decimal SystemQuantity { get; set; }

    /// موجودی شمارش‌شده‌ی فیزیکی توسط کاربر
    public decimal CountedQuantity { get; set; }

    public decimal Discrepancy => CountedQuantity - SystemQuantity;
}
