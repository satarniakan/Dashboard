// Dashboard.Web/Endpoints/ShopOrderEndpoints.cs
using System.Security.Claims;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Endpoints;

/// <summary>
/// ثبت سفارش و چرخه‌ی پرداخت فروشگاه. صفحات Static SSR هستند و فرم‌ها به این Endpointها POST می‌شوند.
/// پرداخت: ثبت سفارش → RequestPayment درگاه → ریدایرکت به درگاه → callback → Verify → تبدیل به فاکتور فروش.
/// </summary>
public static class ShopOrderEndpoints
{
    public static IEndpointRouteBuilder MapShopOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shop/checkout/place", [Authorize] async (
            HttpContext httpContext,
            [FromServices] IOrderService orderService,
            [FromServices] ICartService cartService,
            [FromServices] IPaymentGateway gateway,
            [FromForm] string customerName,
            [FromForm] string customerPhone,
            [FromForm] string province,
            [FromForm] string city,
            [FromForm] string addressLine,
            [FromForm] string? postalCode,
            [FromForm] Domain.Enums.ShippingMethod shippingMethod) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new InvalidOperationException("کاربر بدون شناسه");

            var cookieId = httpContext.Request.Cookies[ShopCartEndpoints.CartCookieName];
            if (string.IsNullOrEmpty(cookieId))
                return Results.Redirect("/shop/cart");

            var dto = new CheckoutDto
            {
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                Province = province,
                City = city,
                AddressLine = addressLine,
                PostalCode = postalCode,
                ShippingMethod = shippingMethod
            };

            var (order, error) = await orderService.PlaceOrderAsync(userId, cookieId, dto);
            if (error is not null)
                return Results.Redirect($"/shop/checkout?error={Uri.EscapeDataString(error)}");

            // درخواست پرداخت از درگاه — آدرس بازگشت مطلق
            var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/shop/payment/callback";
            var payment = await gateway.RequestPaymentAsync(order.Total, $"پرداخت سفارش {order.OrderNumber}", callbackUrl);

            if (!payment.Success || payment.Authority is null)
                return Results.Redirect($"/shop/checkout?error={Uri.EscapeDataString(payment.Error ?? "پرداخت ناموفق")}");

            await orderService.AttachPaymentAsync(order.Id, gateway.Name, order.Total, payment.Authority);

            return Results.Redirect(payment.RedirectUrl!);
        }).RequireRateLimiting("order");

        // بازگشت از درگاه: Authority و Status در کوئری‌استرینگ
        app.MapGet("/shop/payment/callback", async (
            HttpContext httpContext,
            [FromServices] IOrderService orderService,
            [FromServices] ICartService cartService,
            [FromServices] IPaymentGateway gateway,
            [FromQuery] string? Authority,
            [FromQuery] string? Status) =>
        {
            if (string.IsNullOrEmpty(Authority))
                return Results.Redirect("/shop/cart");

            if (!string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                await orderService.MarkPaymentFailedAsync(Authority, "کاربر پرداخت را لغو کرد یا درگاه خطا داد");
                return Results.Redirect("/shop/checkout?error=پرداخت انجام نشد.");
            }

            var order = await orderService.GetByAuthorityAsync(Authority);
            if (order is null)
                return Results.Redirect("/shop/cart?msg=" + Uri.EscapeDataString("سفارش مرتبط با این پرداخت یافت نشد."));

            var verification = await gateway.VerifyPaymentAsync(order.Total, Authority);
            if (!verification.Success)
            {
                await orderService.MarkPaymentFailedAsync(Authority, verification.Error);
                return Results.Redirect($"/shop/checkout?error={Uri.EscapeDataString(verification.Error ?? "تأیید پرداخت ناموفق بود")}");
            }

            var (success, error) = await orderService.MarkPaidAsync(order.Id, Authority, verification.RefId);

            // سبد کاربر پس از ثبت موفق خالی می‌شود
            var cookieId = httpContext.Request.Cookies[ShopCartEndpoints.CartCookieName];
            if (success && !string.IsNullOrEmpty(cookieId))
                await cartService.ClearCartAsync(cookieId);

            if (!success)
                return Results.Redirect($"/shop/orders/{order.Id}?error={Uri.EscapeDataString(error ?? "")}");

            return Results.Redirect($"/shop/orders/{order.Id}?paid=true");
        });

        return app;
    }
}
