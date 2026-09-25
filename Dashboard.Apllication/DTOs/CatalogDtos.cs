using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record CategoryDto(int Id, string Name, string Slug, int? ParentCategoryId);
public class CreateCategoryDto
{
    [Required(ErrorMessage = "نام دسته الزامی است.")]
    [StringLength(150, ErrorMessage = "نام دسته نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسلاگ (Slug) الزامی است.")]
    [StringLength(150, ErrorMessage = "اسلاگ نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    [RegularExpression(@"^[a-z0-9\-]+$",
        ErrorMessage = "اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، عدد و خط تیره باشد.")]
    public string Slug { get; set; } = string.Empty;

    public int? ParentCategoryId { get; set; }
}

public record UnitDto(int Id, string Name, int ProductCount);
public class CreateUnitDto
{
    [Required(ErrorMessage = "نام واحد شمارش الزامی است.")]
    [StringLength(50, ErrorMessage = "نام واحد شمارش نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;
}
public class UpdateUnitDto
{
    [Required(ErrorMessage = "نام واحد شمارش الزامی است.")]
    [StringLength(50, ErrorMessage = "نام واحد شمارش نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;
}

public record AttributeValueDto(int Id, string Value);
public record AttributeDto(int Id, string Name, List<AttributeValueDto> Values);
public class CreateAttributeDto
{
    [Required(ErrorMessage = "نام ویژگی الزامی است.")]
    [StringLength(100, ErrorMessage = "نام ویژگی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    public List<string> Values { get; set; } = new();
}

public record VariantDto(int ProductId, string Sku, string? Barcode, decimal Price, string AttributeSummary);
public record ProductImageDto(int Id, string Url, bool IsPrimary);

public record ProductGroupDto(
    int Id, string Name, string Slug, string? Description,
    string? CategoryName, List<ProductImageDto> Images, List<VariantDto> Variants);

public record ProductGroupSummaryDto(int Id, string Name, string Slug, string? CategoryName, int VariantCount, string? PrimaryImageUrl);

public class CreateProductGroupDto
{
    [Required(ErrorMessage = "نام گروه محصول الزامی است.")]
    [StringLength(200, ErrorMessage = "نام گروه محصول نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسلاگ (Slug) الزامی است.")]
    [StringLength(200, ErrorMessage = "اسلاگ نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    [RegularExpression(@"^[a-z0-9\-]+$",
        ErrorMessage = "اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، عدد و خط تیره باشد.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد.")]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
}

public class CreateVariantDto
{
    public int ProductGroupId { get; set; }

    [Required(ErrorMessage = "کد کالا (SKU) الزامی است.")]
    [StringLength(50, ErrorMessage = "کد کالا نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "بارکد نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Barcode { get; set; }

    [Required(ErrorMessage = "نام متغیر الزامی است.")]
    [StringLength(200, ErrorMessage = "نام متغیر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string VariantName { get; set; } = string.Empty; // مثلا "قرمز - L"

    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت فروش باید بزرگتر از صفر باشد.")]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "بهای تمام‌شده نمی‌تواند منفی باشد.")]
    public decimal CostPrice { get; set; }

    public List<int> AttributeValueIds { get; set; } = new();
}