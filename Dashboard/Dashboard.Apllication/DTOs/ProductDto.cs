// Dashboard.Application/DTOs/ProductDto.cs
using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public record ProductDto(int Id, string Name, decimal Price);

public class CreateProductDto
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت باید بزرگتر از صفر باشد.")]
    public decimal Price { get; set; }
}

public class UpdateProductDto
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت باید بزرگتر از صفر باشد.")]
    public decimal Price { get; set; }
}