using Dashboard.Domain.Exceptions;

namespace Dashboard.Application.Validators;

/// <summary>
/// کلاس کمکی برای انجام اعتبارسنجی‌های مشترک در سراسر برنامه
/// </summary>
public static class CommonValidations
{
    /// <summary>
    /// بررسی اینکه لیست اقلام خالی نباشد
    /// </summary>
    public static void ValidateItemsNotEmpty<T>(List<T> items, string message = "حداقل یک قلم کالا لازم است.")
    {
        if (items.Count == 0)
            throw new BusinessRuleException(message);
    }

    /// <summary>
    /// بررسی اینکه مقدار مثبت باشد
    /// </summary>
    public static void ValidateQuantityPositive(decimal quantity)
    {
        if (quantity <= 0)
            throw new BusinessRuleException("مقدار باید بزرگتر از صفر باشد.");
    }

    /// <summary>
    /// بررسی اینکه مبلغ مثبت باشد
    /// </summary>
    public static void ValidateAmountPositive(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("مبلغ باید بزرگتر از صفر باشد.");
    }

    /// <summary>
    /// بررسی اینکه قیمت منفی نباشد
    /// </summary>
    public static void ValidatePriceNonNegative(decimal price)
    {
        if (price < 0)
            throw new BusinessRuleException("قیمت واحد نمی‌تواند منفی باشد.");
    }
}
