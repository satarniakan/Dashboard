// Dashboard.E2ETests/StorefrontPurchaseTests.cs
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Dashboard.E2ETests;

/// <summary>
/// تست سرتاسری ویترین: جست‌وجوی زنده، افزودن به سبد، ثبت سفارش و پرداخت (درگاه Fake) تا رسید.
/// نیازمند اجرای اپ روی BASE_URL با PaymentGateway:Provider=Fake و Store:WarehouseId=2002.
/// </summary>
[Collection("E2E")]
public class StorefrontPurchaseTests
{
    private readonly BrowserFixture _browsers;
    private readonly E2EDataFixture _db;

    public StorefrontPurchaseTests(BrowserFixture browsers, E2EDataFixture db)
    {
        _browsers = browsers;
        _db = db;
    }

    private static string Url(string path) => E2EConfig.BaseUrl.TrimEnd('/') + path;

    [Fact]
    public async Task LiveSearch_FiltersResults_WithoutPageReload()
    {
        await using var playwrightCtx = await _browsers.NewContextAsync();
        var page = await playwrightCtx.NewPageAsync();

        await page.GotoAsync(Url("/shop/products"));
        await page.WaitForSelectorAsync("#product-results .col-6");

        // پرچم روی window: اگر صفحه واقعاً reload شود پاک می‌شود
        await page.EvaluateAsync("() => { window.__noReload = 'kept'; }");

        await page.FillAsync("#q", "کالای تست");
        await page.WaitForURLAsync(new Regex(@"/shop/products\?q="));

        var names = await page.Locator("#product-results .col-6").AllTextContentsAsync();
        Assert.All(names, n => Assert.Contains("کالای تست", n));
        Assert.Equal(2, names.Count);

        // جست‌وجوی بی‌نتیجه → حالت خالی
        await page.FillAsync("#q", "zzz-ناموجود-zzz");
        await page.WaitForFunctionAsync(
            "() => document.getElementById('product-results')?.textContent.includes('پیدا نشد') ?? false");
        var noReload = await page.EvaluateAsync<string?>("() => window.__noReload ?? null");
        Assert.Equal("kept", noReload);
    }

    [Fact]
    public async Task PurchaseFlow_AddToCart_Checkout_PaidReceipt()
    {
        await using var playwrightCtx = await _browsers.NewContextAsync();
        var page = await playwrightCtx.NewPageAsync();

        // ۱) ورود OTP با شماره مشتری تستی (کد از دیتابیس خوانده می‌شود)
        await page.GotoAsync(Url("/login"));
        await page.FillAsync("input[name='phoneNumber']", E2EConfig.CustomerPhone);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync(new Regex("/verify-otp"));

        var otp = await _db.ReadOtpCodeAsync(E2EConfig.CustomerPhone);
        await page.FillAsync("input[name='code']", otp);
        await page.ClickAsync("button[type='submit']");
        // کاربر جدید → /profile?welcome=true، کاربر قدیمی → /
        await page.WaitForFunctionAsync("() => !location.pathname.startsWith('/verify-otp')");

        // ۲) افزودن کالا به سبد از صفحه جزئیات
        await page.GotoAsync(Url("/shop/p/e2e-tost-1"));
        await page.WaitForSelectorAsync("form[action='/shop/cart/add'] button[type='submit']");
        await page.FillAsync("input[name='quantity']", "2");
        await page.ClickAsync("form[action='/shop/cart/add'] button[type='submit']");

        // ۳) سبد خرید: خط کالا با تعداد ۲
        await page.GotoAsync(Url("/shop/cart"));
        await page.WaitForSelectorAsync("td a[href='/shop/p/e2e-tost-1']");
        var cartText = await page.Locator("body").InnerTextAsync();
        Assert.Contains("کالای تست فروشگاه یک", cartText);

        // ۴) تکمیل چک‌اوت و پرداخت (درگاه Fake مستقیماً به callback محلی می‌رود)
        await page.ClickAsync("a[href='/shop/checkout']");
        await page.WaitForURLAsync(new Regex("/shop/checkout"));
        await page.FillAsync("input[name='customerName']", "مشتری تست");
        await page.FillAsync("input[name='customerPhone']", E2EConfig.CustomerPhone);
        await page.FillAsync("input[name='province']", "تهران");
        await page.FillAsync("input[name='city']", "تهران");
        await page.FillAsync("textarea[name='addressLine']", "آدرس نمونه تست E2E");
        await page.FillAsync("input[name='postalCode']", "1234567890");
        await page.CheckAsync("#ship-post");
        await page.ClickAsync("button[type='submit']");

        // ۵) رسید سفارش با پرداخت موفق
        await page.WaitForURLAsync(new Regex(@"/shop/orders/\d+\?paid=true"), new() { Timeout = 30_000 });
        await page.WaitForSelectorAsync(".alert-success");
        var receipt = await page.Locator("body").InnerTextAsync();
        Assert.Contains("پرداخت شما با موفقیت انجام شد", receipt);
        await page.WaitForSelectorAsync("button:has-text('چاپ رسید')"); // دکمه چاپ رسید
        Assert.Contains("کالای تست فروشگاه یک", receipt);

        // ۶) لیست سفارش‌های کاربر شامل همین سفارش با وضعیت پرداخت‌شده
        var orderId = Regex.Match(page.Url, @"/shop/orders/(\d+)").Groups[1].Value;
        await page.GotoAsync(Url("/shop/orders"));
        await page.WaitForSelectorAsync($"a[href='/shop/orders/{orderId}']");

        // ۷) سبد پس از پرداخت خالی شده است
        await page.GotoAsync(Url("/shop/cart"));
        await page.WaitForSelectorAsync("text=سبد خرید شما خالی است");
    }
}
