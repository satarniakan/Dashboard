namespace Dashboard.Domain.Entities;

// موجودی فعلی هر کالا در هر انبار (کش سریع؛ منبع حقیقت اصلی StockMovement است)
public class ProductStock
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}