namespace Dashboard.Domain.Entities;

/// <summary>ثبت ضایعات — سرِ سند</summary>
public class ScrapRecord
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string RecordNumber { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ScrapRecordItem> Items { get; set; } = new();
}

public class ScrapRecordItem
{
    public int Id { get; set; }
    public int ScrapRecordId { get; set; }
    public ScrapRecord? ScrapRecord { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
}
