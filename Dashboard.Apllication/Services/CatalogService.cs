using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface ICatalogService
{
    Task CreateCategoryAsync(CreateCategoryDto dto);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();

    Task CreateAttributeAsync(CreateAttributeDto dto);
    Task<IEnumerable<AttributeDto>> GetAttributesAsync();

    Task<int> CreateProductGroupAsync(CreateProductGroupDto dto);
    Task<IEnumerable<ProductGroupSummaryDto>> GetProductGroupsAsync();
    Task<ProductGroupDto?> GetProductGroupAsync(int id);

    Task AddVariantAsync(CreateVariantDto dto);
    Task AddImageAsync(int productGroupId, string url, bool isPrimary);
}

public class CatalogService : ICatalogService
{
    private readonly IUnitOfWork _unitOfWork;

    public CatalogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task CreateCategoryAsync(CreateCategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Slug = dto.Slug,
            ParentCategoryId = dto.ParentCategoryId
        };
        await _unitOfWork.Catalog.AddCategoryAsync(category);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.Catalog.GetCategoriesAsync();
        return categories.Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentCategoryId));
    }

    public async Task CreateAttributeAsync(CreateAttributeDto dto)
    {
        var attribute = new ProductAttribute { Name = dto.Name };
        foreach (var value in dto.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            attribute.Values.Add(new ProductAttributeValue { Value = value.Trim() });
        }
        await _unitOfWork.Catalog.AddAttributeAsync(attribute);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<IEnumerable<AttributeDto>> GetAttributesAsync()
    {
        var attributes = await _unitOfWork.Catalog.GetAttributesAsync();
        return attributes.Select(a => new AttributeDto(
            a.Id, a.Name, a.Values.Select(v => new AttributeValueDto(v.Id, v.Value)).ToList()));
    }

    public async Task<int> CreateProductGroupAsync(CreateProductGroupDto dto)
    {
        var group = new ProductGroup
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            CategoryId = dto.CategoryId
        };
        await _unitOfWork.Catalog.AddGroupAsync(group);
        await _unitOfWork.CompleteAsync();
        return group.Id;
    }

    public async Task<IEnumerable<ProductGroupSummaryDto>> GetProductGroupsAsync()
    {
        var groups = await _unitOfWork.Catalog.GetGroupsAsync();
        return groups.Select(g => new ProductGroupSummaryDto(
            g.Id, g.Name, g.Slug, g.Category?.Name, g.Variants.Count,
            g.Images.OrderByDescending(i => i.IsPrimary).FirstOrDefault()?.Url));
    }

    public async Task<ProductGroupDto?> GetProductGroupAsync(int id)
    {
        var group = await _unitOfWork.Catalog.GetGroupByIdAsync(id);
        if (group is null) return null;

        var variantDtos = new List<VariantDto>();
        foreach (var variant in group.Variants)
        {
            // برای هر Variant، ترکیب ویژگی‌هایش را برای نمایش می‌سازیم
            variantDtos.Add(new VariantDto(variant.Id, variant.Sku, variant.Barcode, variant.Price, variant.Name));
        }

        return new ProductGroupDto(
            group.Id, group.Name, group.Slug, group.Description, group.Category?.Name,
            group.Images.Select(i => new ProductImageDto(i.Id, i.Url, i.IsPrimary)).ToList(),
            variantDtos);
    }

    public async Task AddVariantAsync(CreateVariantDto dto)
    {
        var product = new Product(
            sku: dto.Sku,
            name: dto.VariantName,
            price: dto.Price,
            costPrice: dto.CostPrice,
            barcode: dto.Barcode);

        product.AssignToGroup(dto.ProductGroupId);

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.CompleteAsync(); // برای گرفتن Id واقعی محصول قبل از لینک‌کردن ویژگی‌ها

        foreach (var attributeValueId in dto.AttributeValueIds)
        {
            await _unitOfWork.Catalog.AddVariantAttributeAsync(new ProductVariantAttribute
            {
                ProductId = product.Id,
                ProductAttributeValueId = attributeValueId
            });
        }
        await _unitOfWork.CompleteAsync();
    }

    public async Task AddImageAsync(int productGroupId, string url, bool isPrimary)
    {
        await _unitOfWork.Catalog.AddImageAsync(new ProductImage
        {
            ProductGroupId = productGroupId,
            Url = url,
            IsPrimary = isPrimary
        });
        await _unitOfWork.CompleteAsync();
    }
}