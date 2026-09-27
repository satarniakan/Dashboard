using Dashboard.Domain.Entities;
using Xunit;

namespace Dashboard.Tests;

/// <summary>
/// فرمول هزینه‌یابی میانگین موزون — منطق دامنه، مستقل از دیتابیس.
/// </summary>
public class ProductCostTests
{
    private static Product WithCost(decimal cost) => new("SKU-1", "کالای تست", 100, cost);

    [Fact]
    public void FirstPurchase_SetsCostToPurchasePrice()
    {
        var product = WithCost(0);

        product.ApplyWeightedAverageCost(previousQuantity: 0, incomingQuantity: 10, incomingUnitCost: 60_000);

        Assert.Equal(60_000m, product.CostPrice);
    }

    [Fact]
    public void EqualQuantities_AverageOfTwoPrices()
    {
        var product = WithCost(60_000);

        // (۱۰ × ۶۰٬۰۰۰ + ۱۰ × ۸۰٬۰۰۰) ÷ ۲۰
        product.ApplyWeightedAverageCost(10, 10, 80_000);

        Assert.Equal(70_000m, product.CostPrice);
    }

    [Fact]
    public void PurchaseAfterPartialSale_UsesRemainingStockValue()
    {
        var product = WithCost(60_000);

        // ۶ عدد باقی‌مانده با میانگین ۶۰ (ارزش ۳۶۰) + خرید ۱۰ عدد × ۸۰ (ارزش ۸۰۰) ⇒ ۱٬۱۶۰ ÷ ۱۶
        product.ApplyWeightedAverageCost(6, 10, 80_000);

        Assert.Equal(72_500m, product.CostPrice);
    }

    [Fact]
    public void NegativePreviousQuantity_Throws()
    {
        var product = WithCost(60_000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => product.ApplyWeightedAverageCost(-1, 10, 80_000));
    }

    [Fact]
    public void NegativeIncomingCost_Throws()
    {
        var product = WithCost(60_000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => product.ApplyWeightedAverageCost(10, 10, -1));
    }
}
