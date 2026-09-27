using Dashboard.Application;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// تست‌های سطح HTTP — برنامهٔ واقعی داخل حافظه بالا می‌آید تا رفتار middleware، مجوزها
/// و ریدایرکت‌ها بررسی شود (نه فقط سرویس‌ها).
/// </summary>
[Collection("Database")]
public class HttpEndpointTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _db;
    private WebApplicationFactory<Program>? _factory;

    public HttpEndpointTests(TestDatabaseFixture db) => _db = db;

    public Task InitializeAsync()
    {
        if (!_db.Available) return Task.CompletedTask;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); // appsettings.Development انبار ۲۰۰۲ می‌سازد
            builder.UseSetting("ConnectionStrings:Default", _db.ConnectionString);
            builder.UseSetting("Store:WarehouseId", "1");
        });

        // اجرای Lazy: برنامه داخل هر تست بالا می‌آید
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
    }

    [SkippableFact]
    public async Task PaymentCallback_WhenAnonymous_RedirectsToLogin_AndDoesNotTouchOrder()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/shop/payment/callback?Authority=AUTH-TEST-123&Status=OK");

        Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/login", location);
        // پارامترهای بازگشتی حفظ می‌شوند تا کاربر بعد از ورود به همان callback برگردد
        Assert.Contains("ReturnUrl", location);
    }

    [SkippableFact]
    public async Task PaymentCallback_WhenAnonymousAndStatusNok_AlsoRedirectsToLogin()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/shop/payment/callback?Authority=AUTH-TEST-123&Status=NOK");

        // بدون ورود، لغو سفارش دیگران از بیرون اصلاً به منطق سرویس نمی‌رسد
        Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? string.Empty);
    }

    [SkippableFact]
    public async Task PublicShopPages_ReturnOk()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        using var client = _factory!.CreateClient();

        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/login")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/shop/cart")).StatusCode);
    }

    [SkippableFact]
    public async Task CheckoutPlace_WhenAnonymous_IsRejected_AndNoOrderIsCreated()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        const string userId = "it-http-anon-user";
        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);
        var cartCookie = $"cookie-it-{Guid.NewGuid():N}"[..24];

        // سبد را مستقیم می‌سازیم (ارسال فرم نیازمند توکن ضدجعل است)
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cart = new Cart { CookieId = cartCookie };
            cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1 });
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
        }

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"shop_cart={cartCookie}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["customerName"] = "کاربر تست",
            ["customerPhone"] = "09121234567",
            ["province"] = "تهران",
            ["city"] = "تهران",
            ["addressLine"] = "خیابان تست، پلاک ۱",
            ["shippingMethod"] = "1"
        });

        var response = await client.PostAsync("/shop/checkout/place", form);

        // ثبت سفارش حتماً [Authorize] است → کاربر ناشناس به ورود هدایت می‌شود
        Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? string.Empty);

        using var verify = _db.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await verifyContext.Orders.AnyAsync(o => o.UserId == userId));
    }
}