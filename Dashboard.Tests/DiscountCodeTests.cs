// Dashboard.Tests/DiscountCodeTests.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;

namespace Dashboard.Tests;

/// <summary>تست‌های محاسبه و اعتبار کد تخفیف — منطق محض دامنه، بدون دیتابیس</summary>
public class DiscountCodeTests
{
    private static DiscountCode Percentage(decimal percent, decimal? cap = null) => new()
    {
        Code = "TEST", Type = DiscountType.Percentage, Value = percent, MaxDiscountAmount = cap
    };

    private static DiscountCode Fixed(decimal amount) => new()
    {
        Code = "TEST", Type = DiscountType.FixedAmount, Value = amount
    };

    [Fact]
    public void Percentage_Discounts_Proportional_To_Cart()
    {
        var code = Percentage(20);
        Assert.Equal(20_000m, code.CalculateDiscount(100_000m));
    }

    [Fact]
    public void Percentage_Caps_at_max_discount_amount()
    {
        var code = Percentage(50, cap: 30_000m);
        Assert.Equal(30_000m, code.CalculateDiscount(100_000m));
    }

    [Fact]
    public void Fixed_amount_never_exceeds_cart_total()
    {
        var code = Fixed(500_000m);
        Assert.Equal(100_000m, code.CalculateDiscount(100_000m));
    }

    [Fact]
    public void Min_cart_amount_blocks_small_carts()
    {
        var code = Fixed(10_000m);
        code.MinCartAmount = 200_000m;
        Assert.Equal(0m, code.CalculateDiscount(100_000m));
        Assert.Equal(10_000m, code.CalculateDiscount(300_000m));
    }

    [Fact]
    public void Inactive_code_is_invalid()
    {
        var code = Percentage(10);
        code.IsActive = false;
        Assert.False(code.IsValidNow(DateTime.UtcNow));
    }

    [Fact]
    public void Expired_code_is_invalid()
    {
        var code = Percentage(10);
        code.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        Assert.False(code.IsValidNow(DateTime.UtcNow));

        code.ExpiresAt = DateTime.UtcNow.AddDays(1);
        Assert.True(code.IsValidNow(DateTime.UtcNow));
    }

    [Fact]
    public void Not_yet_started_code_is_invalid()
    {
        var code = Percentage(10);
        code.StartsAt = DateTime.UtcNow.AddDays(1);
        Assert.False(code.IsValidNow(DateTime.UtcNow));
    }

    [Fact]
    public void Max_usage_count_is_enforced()
    {
        var code = Percentage(10);
        code.MaxUsageCount = 5;
        code.UsageCount = 5;
        Assert.False(code.IsValidNow(DateTime.UtcNow));

        code.UsageCount = 4;
        Assert.True(code.IsValidNow(DateTime.UtcNow));
    }

}
