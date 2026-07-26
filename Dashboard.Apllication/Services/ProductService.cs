// Dashboard.Application/Services/ProductService.cs
using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IProductService
{
    Task<ProductDto?> GetProductAsync(int id);
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task<ProductDto> CreateProductAsync(CreateProductDto dto);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository repository,
        IAuditService auditService,
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductAsync(int id)
    {
        var product = await _repository.GetByIdAsync(id);
        return product is null ? null : new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _repository.GetAllAsync();
        return products.Select(p => new ProductDto(p.Id, p.Name, p.Price));
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
    {
        var product = new Product(dto.Name, dto.Price);
        await _repository.AddAsync(product);

        await _auditService.LogEventAsync(
            "ProductCreated",
            null,
            $"Product '{product.Name}' (Id: {product.Id}) created with price {product.Price}");

        _logger.LogInformation("Product {ProductId} created", product.Id);

        return new ProductDto(product.Id, product.Name, product.Price);
    }
}