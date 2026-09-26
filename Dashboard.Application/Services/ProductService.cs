using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IProductService
{
    Task<ProductDto?> GetProductAsync(int id);
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task<PagedResult<ProductDto>> GetProductsPagedAsync(int page, int pageSize, string? search = null);
    Task<ProductDto> CreateProductAsync(CreateProductDto dto, string? userEmail);
    Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, string? userEmail);
    Task<bool> DeleteProductAsync(int id, string? userEmail);
}
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

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Price, p.Sku, p.Barcode, p.Unit, p.CostPrice,
        p.Weight, p.Length, p.Width, p.Height, p.ReorderPoint,
        p.CategoryId, p.Category?.Name, p.ImageUrl,
        p.IsPublished, p.Slug, p.HtmlDescription);

    public async Task<ProductDto?> GetProductAsync(int id)
    {
        // استفاده از ریپازیتوریِ داخلِ UnitOfWork
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        return product is null ? null : ToDto(product);
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        return products.Select(ToDto);
    }

    public async Task<PagedResult<ProductDto>> GetProductsPagedAsync(int page, int pageSize, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var (items, totalCount) = await _unitOfWork.Products.GetPagedAsync(page, pageSize, search);

        return new PagedResult<ProductDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, string? userEmail)
    {
        // سازنده‌ی کامل: هم فیلدهای اصلی (نام/قیمت) و هم فیلدهای انبارداری را یک‌جا تنظیم می‌کند
        var product = new Product(
            sku: dto.Sku,
            name: dto.Name,
            price: dto.Price,
            costPrice: dto.CostPrice,
            unit: dto.Unit,
            barcode: dto.Barcode,
            weight: dto.Weight,
            length: dto.Length,
            width: dto.Width,
            height: dto.Height,
            reorderPoint: dto.ReorderPoint);

        product.SetCategory(dto.CategoryId);
        product.SetImage(dto.ImageUrl);
        await ApplyStoreDetailsAsync(product, dto.IsPublished, dto.Slug, dto.HtmlDescription, excludeProductId: null);

        await _unitOfWork.Products.AddAsync(product);

        // لاگ کردن به جای IAuditService، مستقیماً از طریق UnitOfWork
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductCreated", userEmail, $"Product '{product.Name}' created."));

        // مهم‌ترین بخش: ذخیره نهایی کل تراکنش
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("Product {ProductId} created by {UserEmail}", product.Id, userEmail ?? "unknown");
        return ToDto(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, string? userEmail)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null) return null;

        product.Update(dto.Name, dto.Price);
        product.UpdateWarehouseDetails(
            sku: dto.Sku,
            barcode: dto.Barcode,
            unit: dto.Unit,
            costPrice: dto.CostPrice,
            weight: dto.Weight,
            length: dto.Length,
            width: dto.Width,
            height: dto.Height,
            reorderPoint: dto.ReorderPoint);
        product.SetCategory(dto.CategoryId);
        product.SetImage(dto.ImageUrl);
        await ApplyStoreDetailsAsync(product, dto.IsPublished, dto.Slug, dto.HtmlDescription, excludeProductId: id);

        await _unitOfWork.Products.UpdateAsync(product);

        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductUpdated", userEmail, $"Product {id} updated."));

        // ذخیره نهایی
        await _unitOfWork.CompleteAsync();

        return ToDto(product);
    }

    // اعتبارسنجی یکتایی Slug و اعمال فیلدهای فروشگاه؛ خطای تکراری‌بودن با پیام فارسی
    private async Task ApplyStoreDetailsAsync(Product product, bool isPublished, string? slug, string? htmlDescription, int? excludeProductId)
    {
        var normalized = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();
        if (normalized is not null)
        {
            var duplicate = await _unitOfWork.Products.GetBySlugAsync(normalized);
            if (duplicate is not null && duplicate.Id != excludeProductId)
                throw new BusinessRuleException("این نشان (Slug) قبلاً برای کالای دیگری ثبت شده است.");
        }
        product.SetStoreDetails(isPublished, normalized, htmlDescription);
    }

    public async Task<bool> DeleteProductAsync(int id, string? userEmail)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null) return false;

        await _unitOfWork.Products.DeleteAsync(id);

        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ProductDeleted", userEmail, $"Product {id} deleted."));

        // ذخیره نهایی — اگر این محصول به رکورد دیگری وابسته باشد،
        // CompleteAsync خودش BusinessRuleException با پیام فارسی پرتاب می‌کند
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
