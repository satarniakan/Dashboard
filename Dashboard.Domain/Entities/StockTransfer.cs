using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>انتقال کالا بین دو انبار — سرِ سند</summary>
public class StockTransfer
{
    public int Id { get; set; }
    public int SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }
    public int DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Pending;
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<StockTransferItem> Items { get; set; } = new();
}

public class StockTransferItem
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
}
