namespace Dashboard.Application.DTOs;

public record StockLevelDto(
    int ProductId,
    string ProductName,
    string Sku,
    int WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    int ReorderPoint);

public record StockTransactionDto(
    int Id,
    string ProductName,
    string WarehouseName,
    string Type,
    decimal QuantityChange,
    decimal? UnitCost,
    string? Notes,
    string? CreatedByUserId,
    DateTime OccurredAt);

public class StockItemInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class PurchaseReceiptItemInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class CreatePurchaseReceiptDto
{
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<PurchaseReceiptItemInput> Items { get; set; } = new();
}

public class CreateInternalIssueDto
{
    public int WarehouseId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public List<StockItemInput> Items { get; set; } = new();
}

public class CreateSalesReturnDto
{
    public int WarehouseId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public string? CustomerReference { get; set; }
    public string? Notes { get; set; }
    public List<StockItemInput> Items { get; set; } = new();
}

public class CreateScrapRecordDto
{
    public int WarehouseId { get; set; }
    public string RecordNumber { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public List<StockItemInput> Items { get; set; } = new();
}

public class CreateStockTransferDto
{
    public int SourceWarehouseId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<StockItemInput> Items { get; set; } = new();
}

public record StockCountItemDto(int ProductId, string ProductName, decimal SystemQuantity, decimal? CountedQuantity);

public record StockCountDto(int Id, string CountNumber, string WarehouseName, string Status, DateTime CountDate, List<StockCountItemDto> Items);
