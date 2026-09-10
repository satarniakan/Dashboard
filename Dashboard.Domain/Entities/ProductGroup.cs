namespace Dashboard.Domain.Entities;

/// <summary>گروه محصول — چیزی که مشتری در فروشگاه می‌بیند (مثلاً «تی‌شرت مردانه»)؛ هر Variant آن یک Product جداست</summary>
public class ProductGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Product> Variants { get; set; } = new();
    public List<ProductImage> Images { get; set; } = new();
}