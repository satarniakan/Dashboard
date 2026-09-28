using Dashboard.Application.Helpers;
using Xunit;

namespace Dashboard.Tests;

/// <summary>
/// «منهای» و «هزار میلیارد» پیش از این نبودند: مبلغ منفی رشتهٔ خالی می‌داد و
/// ۱۰¹² با شاخصِ خارج از محدوده (Tens[100]) پرتاب می‌شد.
/// </summary>
public class PersianNumberToWordsTests
{
    [Theory]
    // رفتار پایه نباید عوض شود
    [InlineData(0L, "صفر")]
    [InlineData(1000L, "هزار")]
    [InlineData(1_001_000L, "یک میلیون و یک هزار")]
    [InlineData(12_345L, "دوازده هزار و سیصد و چهل و پنج")]
    public void ToWords_BaseContract_Unchanged(long input, string expected)
        => Assert.Equal(expected, PersianNumberToWords.ToWords(input));

    [Fact]
    public void ToWords_Negative_IsPrefixedWithManhaei_NotEmpty()
    {
        var words = PersianNumberToWords.ToWords(-4500);

        Assert.Equal("منهای چهار هزار و پانصد", words);
        Assert.False(string.IsNullOrWhiteSpace(words));
    }

    [Fact]
    public void ToTomanWords_Negative_KeepsSuffix()
        => Assert.Equal("منهای چهار هزار و پانصد تومان", PersianNumberToWords.ToTomanWords(-4500));

    [Fact]
    public void ToWords_OneTrillion_DoesNotThrow()
        => Assert.Equal("یک هزار میلیارد", PersianNumberToWords.ToWords(1_000_000_000_000L));

    [Fact]
    public void ToWords_LargestSupportedGrouping_UsesAllScales()
        => Assert.Equal("نهصد و نود و نه هزار میلیارد", PersianNumberToWords.ToWords(999_000_000_000_000L));

    [Fact]
    public void ToWords_BeyondTable_ReturnsDigitsInsteadOfThrowing()
    {
        // این تابع هنگام تایپ در AppPriceField صدا زده می‌شود؛ پس از سقف، رقم می‌دهیم نه exception
        var words = PersianNumberToWords.ToWords(long.MaxValue);

        Assert.DoesNotContain("هزار میلیارد و", words);
        Assert.Contains(",", words); // گروه‌های رقمی
    }

    [Fact]
    public void ToWords_LongMinValue_DoesNotOverflowOnNegation()
    {
        var words = PersianNumberToWords.ToWords(long.MinValue);

        Assert.StartsWith("منهای", words);
    }
}
