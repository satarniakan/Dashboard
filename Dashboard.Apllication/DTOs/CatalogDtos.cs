namespace Dashboard.Application.DTOs;

public record CategoryDto(int Id, string Name, string Slug, int? ParentCategoryId);
public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
}

public record AttributeValueDto(int Id, string Value);
public record AttributeDto(int Id, string Name, List<AttributeValueDto> Values);
public class CreateAttributeDto
{
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
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
}

public class CreateVariantDto
{
    public int ProductGroupId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string VariantName { get; set; } = string.Empty; // مثلا "قرمز - L"
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public List<int> AttributeValueIds { get; set; } = new();
}