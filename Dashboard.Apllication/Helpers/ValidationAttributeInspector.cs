// Dashboard.Application/Helpers/ValidationAttributeInspector.cs
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Dashboard.Application.Helpers;

/// <summary>
/// بررسی می‌کند فیلدِ متصل‌شده به کامپوننت‌های فرم، بر اساس attribute های اعتبارسنجیِ DTO
/// «الزامی» است یا نه؛ تا ستاره‌ی کنار برچسب خودکار (بدون تنظیم دستی Required) نمایش داده شود.
/// </summary>
public static class ValidationAttributeInspector
{
    // بازتاب در هر رندر هزینه دارد؛ نتیجه برای هر خاصیت کش می‌شود.
    private static readonly ConcurrentDictionary<PropertyInfo, bool> Cache = new();

    public static bool IsRequired(LambdaExpression? expression)
    {
        var property = GetProperty(expression);
        return property is not null && Cache.GetOrAdd(property, static p => ComputeIsRequired(p));
    }

    /// <summary>
    /// نمونه‌ی lambda ای که کامپوننت‌ها در ValueExpression می‌گیرند، معمولاً به یک خاصیت اشاره می‌کند
    /// (گاهی با یک Convert برای Nullable)؛ همان PropertyInfo را برمی‌گردانیم.
    /// </summary>
    public static PropertyInfo? GetProperty(LambdaExpression? expression)
    {
        if (expression is null) return null;

        var body = expression.Body is UnaryExpression unary ? unary.Operand : expression.Body;
        return (body as MemberExpression)?.Member as PropertyInfo;
    }

    private static bool ComputeIsRequired(PropertyInfo property) =>
        property.GetCustomAttribute<RequiredAttribute>() is not null ||
        HasPositiveMinimum(property.GetCustomAttribute<RangeAttribute>());

    // [Range] با حداقلِ بزرگتر از صفر یعنی مقدار پیش‌فرض (صفر) معتبر نیست؛ پس فیلد عملاً الزامی است.
    // مثال: Range(1, …) برای شناسه‌ها و Range(0.01, …) برای مبالغ → ستاره می‌گیرند،
    // ولی Range(0, …) (مثلاً تخفیف یا بهای تمام‌شده) ستاره نمی‌گیرد.
    private static bool HasPositiveMinimum(RangeAttribute? range)
    {
        if (range is null) return false;
        try
        {
            return Convert.ToDecimal(range.Minimum, CultureInfo.InvariantCulture) > 0m;
        }
        catch (Exception)
        {
            return false;
        }
    }
}