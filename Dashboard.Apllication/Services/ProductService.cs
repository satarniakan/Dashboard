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
    Task<ProductDto> CreateProductAsync(CreateProductDto dto, string? userEmail);
    Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, string? userEmail);
    Task<bool> DeleteProductAsync(int id, string? userEmail);
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

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, string? userEmail)
    {
        var product = new Product(dto.Name, dto.Price);
        await _repository.AddAsync(product);

        await _auditService.LogEventAsync(
            "ProductCreated",
            userEmail,
            $"Product '{product.Name}' (Id: {product.Id}) created with price {product.Price}");

        _logger.LogInformation("Product {ProductId} created by {UserEmail}", product.Id, userEmail ?? "unknown");

        return new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, string? userEmail)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return null;
        }

        var oldName = product.Name;
        var oldPrice = product.Price;

        product.Update(dto.Name, dto.Price);
        await _repository.UpdateAsync(product);

        await _auditService.LogEventAsync(
            "ProductUpdated",
            userEmail,
            $"Product (Id: {id}) changed from '{oldName}' ({oldPrice}) to '{product.Name}' ({product.Price})");

        _logger.LogInformation("Product {ProductId} updated by {UserEmail}", id, userEmail ?? "unknown");

        return new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<bool> DeleteProductAsync(int id, string? userEmail)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return false;
        }

        await _repository.DeleteAsync(id);

        await _auditService.LogEventAsync(
            "ProductDeleted",
            userEmail,
            $"Product '{product.Name}' (Id: {id}) deleted");

        _logger.LogInformation("Product {ProductId} deleted by {UserEmail}", id, userEmail ?? "unknown");

        return true;
    }
}