// Dashboard.Domain/Entities/OrderItem.cs
namespace Dashboard.Domain.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>نام کالا هنگام خرید (snapshot)</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>قیمت واحد هنگام خرید (snapshot)</summary>
    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
