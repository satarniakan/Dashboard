// Dashboard.Domain/Entities/CartItem.cs
namespace Dashboard.Domain.Entities;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    // decimal چون واحد شمارش می‌تواند کسری باشد (کیلوگرم، متر و...)
    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
