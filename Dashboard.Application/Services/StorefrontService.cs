// Dashboard.Application/Services/StorefrontService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Dashboard.Application.Services;

public interface IStorefrontService
{
    // فهرست کالاهای منتشرشده با جست‌وجو/دسته‌بندی و صفحه‌بندی
    Task<(IEnumerable<StoreProductDto> Items, int TotalCount)> GetProductsAsync(int page, int pageSize, string? search = null, int? categoryId = null);

    // صفحه‌ی عمومی کالا بر اساس Slug؛ null یعنی منتشر نشده یا وجود ندارد
    Task<StoreProductDetailDto?> GetProductBySlugAsync(string slug);

    // جدیدترین کالاهای منتشرشده برای صفحه‌ی اصلی
    Task<IEnumerable<StoreProductDto>> GetLatestAsync(int count);
}

public class StorefrontService : IStorefrontService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly StoreOptions _store;

    // موجودی ویترین باید دقیقاً همان انباری باشد که سفارش از آن کسر می‌شود،
    // وگرنه کالایی «موجود» دیده می‌شود ولی در سبد «ناموجود» است.
    public StorefrontService(IUnitOfWork unitOfWork, IOptions<StoreOptions> store)
    {
        _unitOfWork = unitOfWork;
        _store = store.Value;
    }

    private static StoreProductDto ToDto(Product p, IReadOnlyDictionary<int, decimal> stock) =>
        new(p.Id, p.Name, p.Slug!, p.Price, p.Unit, p.ImageUrl, p.Category?.Name,
            InStock: stock.TryGetValue(p.Id, out var q) && q > 0);

    public async Task<(IEnumerable<StoreProductDto> Items, int TotalCount)> GetProductsAsync(int page, int pageSize, string? search = null, int? categoryId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 12;

        var (items, totalCount) = await _unitOfWork.Products.GetPublishedPagedAsync(page, pageSize, search, categoryId);
        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(items.Select(p => p.Id).ToList(), _store.WarehouseId);

        return (items.Select(p => ToDto(p, stock)).ToList(), totalCount);
    }

    public async Task<StoreProductDetailDto?> GetProductBySlugAsync(string slug)
    {
        var product = await _unitOfWork.Products.GetPublishedBySlugAsync(slug);
        if (product is null) return null;

        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(new[] { product.Id }, _store.WarehouseId);

        // گالری: عکس خود کالا + عکس‌های گروه محصول (اگر Variant یک گروه باشد)
        var images = new List<string>();
        if (!string.IsNullOrEmpty(product.ImageUrl))
            images.Add(product.ImageUrl);
        if (product.ProductGroup?.Images is { Count: > 0 })
            images.AddRange(product.ProductGroup.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.Url)
                .Where(u => !images.Contains(u)));

        // کالاهای مرتبط: همان دسته‌بندی، به‌جز خود کالا
        var (relatedItems, _) = await _unitOfWork.Products.GetPublishedPagedAsync(1, 5, categoryId: product.CategoryId);
        var related = relatedItems.Where(x => x.Id != product.Id).Take(4).ToList();
        var relatedStock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(related.Select(x => x.Id).ToList(), _store.WarehouseId);

        return new StoreProductDetailDto(
            product.Id, product.Name, product.Slug!, product.Price, product.Unit,
            product.Category?.Name, product.ProductGroup?.Name,
            InStock: stock.TryGetValue(product.Id, out var q) && q > 0,
            product.HtmlDescription, images,
            related.Select(x => ToDto(x, relatedStock)).ToList());
    }

    public async Task<IEnumerable<StoreProductDto>> GetLatestAsync(int count)
    {
        var items = (await _unitOfWork.Products.GetLatestPublishedAsync(count)).ToList();
        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(items.Select(p => p.Id).ToList(), _store.WarehouseId);
        return items.Select(p => ToDto(p, stock));
    }
}
