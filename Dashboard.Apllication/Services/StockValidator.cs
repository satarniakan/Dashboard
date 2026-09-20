using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

/// <summary>
/// سرویس اعتبارسنجی موجودی انبار
/// </summary>
public interface IStockValidator
{
    Task ValidateSufficientStockAsync(int warehouseId, List<StockItemInput> items);
}

/// <summary>
/// پیاده‌سازی سرویس اعتبارسنجی موجودی
/// این کلاس برای جلوگیری از تکرار کد بررسی موجودی در سرویس‌های مختلف ایجاد شده است
/// </summary>
public class StockValidator : IStockValidator
{
    private readonly IUnitOfWork _unitOfWork;

    public StockValidator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// بررسی کفایت موجودی برای اقلام درخواستی در یک انبار
    /// </summary>
    /// <param name="warehouseId">شناسه انبار</param>
    /// <param name="items">لیست اقلام درخواستی</param>
    /// <exception cref="BusinessRuleException">در صورت مقادیر نامعتبر یا عدم کفایت موجودی</exception>
    public async Task ValidateSufficientStockAsync(int warehouseId, List<StockItemInput> items)
    {
        foreach (var item in items)
        {
            CommonValidations.ValidateQuantityPositive(item.Quantity);

            var level = await _unitOfWork.StockLevels.GetAsync(item.ProductId, warehouseId);
            var available = level?.QuantityOnHand ?? 0;

            if (available < item.Quantity)
            {
                var productName = level?.Product?.Name ?? $"شماره {item.ProductId}";
                throw new BusinessRuleException(
                    $"موجودی کافی نیست (کالای «{productName}»: موجود {available}, درخواستی {item.Quantity}).");
            }
        }
    }
}
