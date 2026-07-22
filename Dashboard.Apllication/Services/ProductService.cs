// Dashboard.Application/Services/ProductService.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IProductService
{
    Task<ProductDto?> GetProductAsync(int id);
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();   // NEW
    Task<ProductDto> CreateProductAsync(CreateProductDto dto);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto?> GetProductAsync(int id)
    {
        var product = await _repository.GetByIdAsync(id);
        return product is null ? null : new ProductDto(product.Id, product.Name, product.Price);
    }

    // NEW
    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _repository.GetAllAsync();
        return products.Select(p => new ProductDto(p.Id, p.Name, p.Price));
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
    {
        var product = new Product(dto.Name, dto.Price);
        await _repository.AddAsync(product);
        return new ProductDto(product.Id, product.Name, product.Price);
    }
}