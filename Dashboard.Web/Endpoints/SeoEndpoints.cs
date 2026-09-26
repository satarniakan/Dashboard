// Dashboard.Web/Endpoints/SeoEndpoints.cs
using Dashboard.Application.Services;
using Dashboard.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Endpoints;

/// <summary>
/// SEO: sitemap.xml از روی کالاهای منتشرشده + صفحات ثابت فروشگاه؛ robots.txt ساده.
/// </summary>
public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSitemapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", async (HttpContext httpContext, [FromServices] IStorefrontService storefront) =>
        {
            // sitemap استاندارد آدرس مطلق می‌خواهد
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var (items, _) = await storefront.GetProductsAsync(1, 500);

            var urls = new List<string>
            {
                $"<url><loc>{baseUrl}/shop</loc><priority>1.0</priority></url>",
                $"<url><loc>{baseUrl}/shop/products</loc><priority>0.9</priority></url>"
            };
            urls.AddRange(items.Select(p =>
                $"<url><loc>{baseUrl}/shop/p/{Uri.EscapeDataString(p.Slug)}</loc><priority>0.8</priority></url>"));

            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                      "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                      string.Join("\n", urls) + "\n</urlset>";

            return Results.Text(xml, "application/xml", System.Text.Encoding.UTF8);
        }).ExcludeFromDescription();

        return app;
    }
}
