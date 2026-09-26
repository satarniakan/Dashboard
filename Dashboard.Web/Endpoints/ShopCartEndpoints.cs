// Dashboard.Web/Endpoints/ShopCartEndpoints.cs
using Dashboard.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Endpoints;

/// <summary>
/// عملیات سبد خرید فروشگاه. صفحات فروشگاه Static SSR هستند، پس افزودن/ویرایش سبد
/// با فرم POST به این Endpointها انجام می‌شود (کوکی سبد اینجا ست می‌شود چون داخل
/// Blazor Static امکان‌پذیر نیست). Antiforgery توسط middleware روی فرم‌ها اعمال می‌شود.
/// </summary>
public static class ShopCartEndpoints
{
    public const string CartCookieName = "shop_cart";

    public static IEndpointRouteBuilder MapShopCartEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shop/cart/add", async (
            HttpContext httpContext,
            [FromServices] ICartService cartService,
            [FromForm] int productId,
            [FromForm] decimal quantity,
            [FromForm] string? returnUrl) =>
        {
            var cookieId = GetOrCreateCartCookie(httpContext);
            var result = await cartService.AddToCartAsync(cookieId, productId, quantity);

            return Results.Redirect(BuildRedirect(returnUrl, result.Message));
        });

        app.MapPost("/shop/cart/update", async (
            HttpContext httpContext,
            [FromServices] ICartService cartService,
            [FromForm] int itemId,
            [FromForm] decimal quantity,
            [FromForm] string? returnUrl) =>
        {
            var cookieId = httpContext.Request.Cookies[CartCookieName];
            if (string.IsNullOrEmpty(cookieId))
                return Results.Redirect("/shop/cart");

            var result = await cartService.UpdateQuantityAsync(cookieId, itemId, quantity);
            return Results.Redirect(BuildRedirect(returnUrl, result.Message));
        });

        // شمارنده‌ی سبد برای بَج — fetch از JS (خارج از مدار Blazor، بدون تداخل DbContext)
        app.MapGet("/shop/cart/count", async (
            HttpContext httpContext,
            [FromServices] ICartService cartService) =>
        {
            var cookieId = httpContext.Request.Cookies[CartCookieName];
            var count = string.IsNullOrEmpty(cookieId) ? 0 : await cartService.GetItemCountAsync(cookieId);
            return Results.Json(new { count });
        });

        app.MapPost("/shop/cart/discount", async (
            HttpContext httpContext,
            [FromServices] ICartService cartService,
            [FromForm] string code) =>
        {
            var cookieId = httpContext.Request.Cookies[CartCookieName];
            if (string.IsNullOrEmpty(cookieId))
                return Results.Redirect("/shop/cart");

            var result = await cartService.ApplyDiscountCodeAsync(cookieId, code);
            var message = result.Message is null ? "" : $"?msg={Uri.EscapeDataString(result.Message)}";
            return Results.Redirect("/shop/cart" + message);
        });

        app.MapPost("/shop/cart/discount/remove", async (
            HttpContext httpContext,
            ICartService cartService) =>
        {
            var cookieId = httpContext.Request.Cookies[CartCookieName];
            if (!string.IsNullOrEmpty(cookieId))
                await cartService.RemoveDiscountCodeAsync(cookieId);

            return Results.Redirect("/shop/cart");
        });

        app.MapPost("/shop/cart/remove", async (
            HttpContext httpContext,
            [FromServices] ICartService cartService,
            [FromForm] int itemId) =>
        {
            var cookieId = httpContext.Request.Cookies[CartCookieName];
            if (!string.IsNullOrEmpty(cookieId))
                await cartService.RemoveItemAsync(cookieId, itemId);

            return Results.Redirect("/shop/cart");
        });

        return app;
    }

    /// <summary>مسیر بازگشت را فقط در صورت محلی (داخل سایت) بودن می‌پذیرد — جلوگیری از Open Redirect</summary>
    private static string BuildRedirect(string? returnUrl, string? message)
    {
        var baseUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/shop/cart";
        if (string.IsNullOrEmpty(message)) return baseUrl;
        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}msg={Uri.EscapeDataString(message)}";
    }

    private static bool IsLocalUrl(string? url) =>
        url is not null && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    /// <summary>شناسه‌ی سبد را از کوکی می‌خواند؛ نبود آن را می‌سازد و همیشه کوکی را دوباره ست می‌کند
    /// (تا کوکی‌های قدیمی — مثلاً HttpOnly نسخه‌های قبلی — به تنظیمات جدید ارتقا یابند)</summary>
    public static string GetOrCreateCartCookie(HttpContext httpContext)
    {
        var cookieId = httpContext.Request.Cookies[CartCookieName];
        if (string.IsNullOrEmpty(cookieId))
            cookieId = Guid.NewGuid().ToString("N");

        httpContext.Response.Cookies.Append(CartCookieName, cookieId, new CookieOptions
        {
            // HttpOnly نیست چون بَج تعاملی شمارنده‌ی سبد باید با JS بخواندش؛
            // محتواش فقط GUID تصادفی سبد است و حساسیت ندارد
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(14)
        });
        return cookieId;
    }
}
