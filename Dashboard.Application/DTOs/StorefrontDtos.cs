// Dashboard.Application/DTOs/StorefrontDtos.cs
// DTO های ویترین فروشگاه — عمداً بدون بهای تمام‌شده و اطلاعات محرمانه‌ی back-office
namespace Dashboard.Application.DTOs;

public record StoreProductDto(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    string Unit,
    string? ImageUrl,
    string? CategoryName,
    bool InStock);

public record StoreProductDetailDto(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    string Unit,
    string? CategoryName,
    string? GroupName,
    bool InStock,
    string? HtmlDescription,
    IReadOnlyList<string> Images,
    IReadOnlyList<StoreProductDto> Related);
