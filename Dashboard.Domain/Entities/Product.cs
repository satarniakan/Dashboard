// Dashboard.Domain/Entities/Product.cs
namespace Dashboard.Domain.Entities;

public class Product
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // نگه‌داشته‌شده با همین اسم قبلی (Price) تا سرویس/کنترلر/صفحات Razor فعلی محصولات بدون تغییر کامپایل شوند.
    // از نظر مفهومی همان «قیمت فروش» است.
    public decimal Price { get; private set; }

    // --- فیلدهای جدید مخصوص ماژول انبارداری ---
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string Unit { get; private set; } = "عدد";
    public decimal CostPrice { get; private set; }
    public decimal? Weight { get; private set; }
    public decimal? Length { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Height { get; private set; }
    public int ReorderPoint { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    // اگر این محصول یکی از Variant های یک گروه محصول باشد (برای فروشگاه اینترنتی)، این مقدار پر می‌شود
    public int? ProductGroupId { get; private set; }
    public ProductGroup? ProductGroup { get; private set; }
    private Product() { } // برای EF Core

    // سازنده‌ی قبلی — دست‌نخورده، تا کدهای موجود (ProductService و غیره) کار کنند
    public Product(string name, decimal price)
    {
        if (price < 0) throw new ArgumentException("Price cannot be negative.");
        Name = name;
        Price = price;
        Sku = GenerateFallbackSku();
    }

    // سازنده‌ی کامل برای ثبت کالا با جزئیات انبارداری
    public Product(
        string sku,
        string name,
        decimal price,
        decimal costPrice,
        string unit = "عدد",
        string? barcode = null,
        decimal? weight = null,
        decimal? length = null,
        decimal? width = null,
        decimal? height = null,
        int reorderPoint = 0) : this(name, price)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("کد کالا الزامی است.", nameof(sku));
        if (costPrice < 0) throw new ArgumentException("بهای تمام‌شده نمی‌تواند منفی باشد.");

        Sku = sku;
        CostPrice = costPrice;
        Unit = unit;
        Barcode = barcode;
        Weight = weight;
        Length = length;
        Width = width;
        Height = height;
        ReorderPoint = reorderPoint;
    }

    public void ApplyDiscount(decimal percent)
    {
        Price -= Price * (percent / 100);
    }

    // امضای قبلی — دست‌نخورده
    public void Update(string name, decimal price)
    {
        if (price < 0) throw new ArgumentException("Price cannot be negative.");
        Name = name;
        Price = price;
    }

    // به‌روزرسانی جداگانه‌ی فیلدهای انبارداری — تا وقتی UI محصولات آپدیت شود، بقیه‌ی سیستم دست‌نخورده می‌ماند
    public void UpdateWarehouseDetails(
        string sku,
        string? barcode,
        string unit,
        decimal costPrice,
        decimal? weight,
        decimal? length,
        decimal? width,
        decimal? height,
        int reorderPoint)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("کد کالا الزامی است.", nameof(sku));
        if (costPrice < 0) throw new ArgumentException("بهای تمام‌شده نمی‌تواند منفی باشد.");

        Sku = sku;
        Barcode = barcode;
        Unit = unit;
        CostPrice = costPrice;
        Weight = weight;
        Length = length;
        Width = width;
        Height = height;
        ReorderPoint = reorderPoint;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    // یک محصول موجود را به‌عنوان یکی از Variant های یک گروه محصول علامت‌گذاری می‌کند
    public void AssignToGroup(int productGroupId)
    {
        ProductGroupId = productGroupId;
    }
    private static string GenerateFallbackSku() => $"SKU-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
}
