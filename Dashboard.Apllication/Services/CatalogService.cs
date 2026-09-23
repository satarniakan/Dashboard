using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Domain.Exceptions;
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

    // واحد شمارش
    Task<IEnumerable<UnitDto>> GetUnitsAsync();
    Task CreateUnitAsync(CreateUnitDto dto);
    Task UpdateUnitAsync(int id, UpdateUnitDto dto);
    Task DeleteUnitAsync(int id);
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

    public async Task<IEnumerable<UnitDto>> GetUnitsAsync()
    {
        var units = await _unitOfWork.Catalog.GetUnitsAsync();
        // تعداد کالاهای هر واحد برای نمایش در صفحه و جلوگیری از حذف واحدِ در حال استفاده
        var usage = (await _unitOfWork.Products.GetAllUnitNamesAsync())
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return units
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, usage.GetValueOrDefault(u.Name, 0)));
    }

    public async Task CreateUnitAsync(CreateUnitDto dto)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrEmpty(name))
            throw new BusinessRuleException("نام واحد شمارش الزامی است.");
        if (await _unitOfWork.Catalog.GetUnitByNameAsync(name) is not null)
            throw new BusinessRuleException($"واحد شمارش «{name}» از قبل ثبت شده است.");

        await _unitOfWork.Catalog.AddUnitAsync(new Unit { Name = name });
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateUnitAsync(int id, UpdateUnitDto dto)
    {
        var unit = await _unitOfWork.Catalog.GetUnitByIdAsync(id)
            ?? throw new BusinessRuleException("واحد شمارش مورد نظر یافت نشد.");

        var name = dto.Name.Trim();
        if (string.IsNullOrEmpty(name))
            throw new BusinessRuleException("نام واحد شمارش الزامی است.");
        var duplicate = await _unitOfWork.Catalog.GetUnitByNameAsync(name);
        if (duplicate is not null && duplicate.Id != id)
            throw new BusinessRuleException($"واحد شمارش «{name}» از قبل ثبت شده است.");

        if (unit.Name == name) return;

        // Product.Unit رشته‌ای است؛ تغییر نام واحد باید روی کالاهای موجود هم اعمال شود
        var oldName = unit.Name;
        unit.Name = name;
        foreach (var product in await _unitOfWork.Products.GetByUnitNameAsync(oldName))
        {
            product.RenameUnit(name);
        }
        await _unitOfWork.CompleteAsync();
    }

    public async Task DeleteUnitAsync(int id)
    {
        var unit = await _unitOfWork.Catalog.GetUnitByIdAsync(id)
            ?? throw new BusinessRuleException("واحد شمارش مورد نظر یافت نشد.");

        var inUse = await _unitOfWork.Products.GetByUnitNameAsync(unit.Name);
        if (inUse.Any())
            throw new BusinessRuleException($"{inUse.Count} کالا از واحد «{unit.Name}» استفاده می‌کند؛ اول واحد آن کالاها را تغییر دهید.");

        _unitOfWork.Catalog.RemoveUnit(unit);
        await _unitOfWork.CompleteAsync();
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