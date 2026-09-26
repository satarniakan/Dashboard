// Dashboard.Web/Endpoints/NotificationsEndpoints.cs
using System.Security.Claims;
using Dashboard.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Endpoints;

/// <summary>
/// Endpoint های سبک JSON برای زنگ اعلان‌ها. عمداً از fetch (درخواست HTTP مستقل)
/// استفاده می‌شود نه سرویس مدار — تا هم‌روندی DbContext با صفحات تعاملی پیش نیاید.
/// </summary>
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/recent", [Authorize] async (
            HttpContext httpContext,
            [FromServices] INotificationService notifications) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            return Results.Json(await notifications.GetFeedAsync(userId));
        });

        app.MapPost("/notifications/{id:int}/read", [Authorize] async (
            HttpContext httpContext,
            [FromServices] INotificationService notifications,
            int id) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            await notifications.MarkReadAsync(id, userId);
            return Results.Ok();
        });

        app.MapPost("/notifications/read-all", [Authorize] async (
            HttpContext httpContext,
            [FromServices] INotificationService notifications) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            await notifications.MarkAllReadAsync(userId);
            return Results.Ok();
        });

        return app;
    }
}
