using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public class ProductService : IProductService
{
    // تغییر اصلی: فقط UnitOfWork را داریم
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IUnitOfWork unitOfWork,
        ILogger<ProductService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductAsync(int id)
    {
        // استفاده از ریپازیتوریِ داخلِ UnitOfWork
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        return product is null ? null : new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        return products.Select(p => new ProductDto(p.Id, p.Name, p.Price));
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, string? userEmail)
    {
        var product = new Product(dto.Name, dto.Price);
        await _unitOfWork.Products.AddAsync(product);

        // لاگ کردن به جای IAuditService، مستقیماً از طریق UnitOfWork
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductCreated", userEmail, $"Product '{product.Name}' created."));

        // مهم‌ترین بخش: ذخیره نهایی کل تراکنش
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("Product {ProductId} created by {UserEmail}", product.Id, userEmail ?? "unknown");
        return new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, string? userEmail)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null) return null;

        product.Update(dto.Name, dto.Price);
        await _unitOfWork.Products.UpdateAsync(product);

        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductUpdated", userEmail, $"Product {id} updated."));

        // ذخیره نهایی
        await _unitOfWork.CompleteAsync();

        return new ProductDto(product.Id, product.Name, product.Price);
    }

    public async Task<bool> DeleteProductAsync(int id, string? userEmail)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null) return false;

        await _unitOfWork.Products.DeleteAsync(id);

        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductDeleted", userEmail, $"Product {id} deleted."));

        // ذخیره نهایی
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
