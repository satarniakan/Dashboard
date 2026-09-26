// Dashboard.Application/DTOs/ProductDto.cs
using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record ProductDto(
    int Id,
    string Name,
    decimal Price,
    string Sku,
    string? Barcode,
    string Unit,
    decimal CostPrice,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    int ReorderPoint,
    int? CategoryId,
    string? CategoryName,
    string? ImageUrl,
    bool IsPublished,
    string? Slug,
    string? HtmlDescription);

public class CreateProductDto
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت باید بزرگتر از صفر باشد.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "کد کالا (SKU) الزامی است.")]
    [StringLength(50, ErrorMessage = "کد کالا نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "بارکد نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Barcode { get; set; }

    public string Unit { get; set; } = "عدد";

    [Range(0, double.MaxValue, ErrorMessage = "بهای تمام‌شده نمی‌تواند منفی باشد.")]
    public decimal CostPrice { get; set; }

    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "نقطه سفارش مجدد نمی‌تواند منفی باشد.")]
    public int ReorderPoint { get; set; }

    public int? CategoryId { get; set; }
    public string? ImageUrl { get; set; }

    // --- فروشگاه اینترنتی ---
    public bool IsPublished { get; set; }

    [StringLength(150, ErrorMessage = "نشان (Slug) نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    [RegularExpression(@"^[a-zA-Z0-9\u0600-\u06FF\-._~%]+$", ErrorMessage = "نشان فقط می‌تواند شامل حروف، اعداد و خط تیره باشد.")]
    public string? Slug { get; set; }

    [StringLength(10000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰٬۰۰۰ کاراکتر باشد.")]
    public string? HtmlDescription { get; set; }
}

public class UpdateProductDto
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت باید بزرگتر از صفر باشد.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "کد کالا (SKU) الزامی است.")]
    [StringLength(50, ErrorMessage = "کد کالا نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "بارکد نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string? Barcode { get; set; }

    public string Unit { get; set; } = "عدد";

    [Range(0, double.MaxValue, ErrorMessage = "بهای تمام‌شده نمی‌تواند منفی باشد.")]
    public decimal CostPrice { get; set; }

    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "نقطه سفارش مجدد نمی‌تواند منفی باشد.")]
    public int ReorderPoint { get; set; }

    public int? CategoryId { get; set; }
    public string? ImageUrl { get; set; }

    // --- فروشگاه اینترنتی ---
    public bool IsPublished { get; set; }

    [StringLength(150, ErrorMessage = "نشان (Slug) نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    [RegularExpression(@"^[a-zA-Z0-9\u0600-\u06FF\-._~%]+$", ErrorMessage = "نشان فقط می‌تواند شامل حروف، اعداد و خط تیره باشد.")]
    public string? Slug { get; set; }

    [StringLength(10000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰٬۰۰۰ کاراکتر باشد.")]
    public string? HtmlDescription { get; set; }
}
