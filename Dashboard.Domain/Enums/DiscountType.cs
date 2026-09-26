// Dashboard.Domain/Enums/DiscountType.cs
namespace Dashboard.Domain.Enums;

public enum DiscountType
{
    /// <summary>درصدی — مقدار Value بین ۰ تا ۱۰۰</summary>
    Percentage = 1,

    /// <summary>مبلغ ثابت به تومان</summary>
    FixedAmount = 2
}
