namespace Dashboard.Domain.Entities;

/// <summary>نوع ویژگی متغیر (مثل «رنگ» یا «سایز»)</summary>
public class ProductAttribute
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<ProductAttributeValue> Values { get; set; } = new();
}

/// <summary>مقدار مشخص یک ویژگی (مثل «قرمز» زیرِ ویژگی «رنگ»)</summary>
public class ProductAttributeValue
{
    public int Id { get; set; }

    public int ProductAttributeId { get; set; }
    public ProductAttribute? ProductAttribute { get; set; }

    public string Value { get; set; } = string.Empty;
}

/// <summary>پل بین یک Variant (Product) و ترکیب ویژگی‌هایش — مثلاً Product #101 دارای (رنگ=قرمز) و (سایز=L)</summary>
public class ProductVariantAttribute
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int ProductAttributeValueId { get; set; }
    public ProductAttributeValue? ProductAttributeValue { get; set; }
}