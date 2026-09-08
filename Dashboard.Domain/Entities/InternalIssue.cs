namespace Dashboard.Domain.Entities;

/// <summary>حواله مصرف داخلی — سرِ سند</summary>
public class InternalIssue
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InternalIssueItem> Items { get; set; } = new();
}

public class InternalIssueItem
{
    public int Id { get; set; }
    public int InternalIssueId { get; set; }
    public InternalIssue? InternalIssue { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
}
