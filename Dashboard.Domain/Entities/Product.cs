// Dashboard.Domain/Entities/Product.cs
namespace Dashboard.Domain.Entities;

public class Product
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }

    public Product(string name, decimal price)
    {
        if (price < 0) throw new ArgumentException("Price cannot be negative.");
        Name = name;
        Price = price;
    }

    public void ApplyDiscount(decimal percent)
    {
        Price -= Price * (percent / 100);
    }
}