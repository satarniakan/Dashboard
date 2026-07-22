// Dashboard.Application/DTOs/ProductDto.cs
namespace Dashboard.Application.DTOs;

public record ProductDto(int Id, string Name, decimal Price);
public record CreateProductDto(string Name, decimal Price);