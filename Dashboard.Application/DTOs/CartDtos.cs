// Dashboard.Application/DTOs/CartDtos.cs
namespace Dashboard.Application.DTOs;

public record CartLineDto(
    int ItemId,
    int ProductId,
    string Name,
    string Slug,
    string Unit,
    decimal Price,
    string? ImageUrl,
    decimal Quantity,
    decimal LineTotal,
    decimal AvailableStock);

public record CartDto(
    IReadOnlyList<CartLineDto> Items,
    int TotalItems,
    decimal TotalAmount,
    string? DiscountCode = null,
    decimal DiscountAmount = 0m,
    decimal FinalAmount = 0m);

public record CartOperationResult(bool Success, string? Message = null);
