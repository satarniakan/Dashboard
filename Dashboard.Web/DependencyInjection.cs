// Dashboard.Web/DependencyInjection.cs
using System.Threading.RateLimiting;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Services;
using Dashboard.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

namespace Dashboard.Web;

/// <summary>
/// سرویس‌هایی که مخصوص لایه‌ی Web هستند (Blazor، Authorization، RateLimiter، ...).
/// این‌ها به Application/Infrastructure تعلق ندارند چون به ASP.NET Core Pipeline وابسته‌اند.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IWebHostEnvironment environment)
    {
        // Razor Components (Blazor Server)
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Required for Blazor Server to flow auth state into components
        services.AddCascadingAuthenticationState();

        services.AddPermissionPolicies();
        services.AddRequestRateLimiting();

        // Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(environment.ContentRootPath, "DataProtection-Keys")));

        // --- سرویس‌های UI و فایل ---
        services.AddScoped<ToastService>();
        services.AddSingleton<IFileStorageService>(_ => new LocalFileStorageService(environment.WebRootPath));


        return services;
    }

    // ---------------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------------

    /// <summary>
    /// مجوزهایی که «ضمناً» دسترسی مشاهده را هم می‌دهند.
    /// مثلاً کسی که رسید خرید ثبت می‌کند، حتماً باید موجودی انبار را هم ببیند.
    /// </summary>
    private static readonly Dictionary<string, string[]> ImpliedBy = new()
    {
        [Permissions.ProductsView] = [Permissions.ProductsManage],
        [Permissions.AccountingView] = [Permissions.TreasuryManage],
        [Permissions.StockView] =
        [
            Permissions.PurchaseReceiptsManage, Permissions.InternalIssuesManage,
            Permissions.SalesReturnsManage, Permissions.ScrapRecordsManage,
            Permissions.StockTransfersManage, Permissions.StockCountsManage
        ],
        [Permissions.SalesView] =
        [
            Permissions.SalesCreate, Permissions.SalesConfirm,
            Permissions.SalesCancel, Permissions.CustomersManage
        ]
    };

    /// <summary>
    /// برای هر مجوز موجود در Permissions.All یک Policy هم‌نام می‌سازد؛ کاربر باید همان Claim را
    /// داشته باشد یا یکی از مجوزهای «ضمنی» (ImpliedBy) آن را.
    /// </summary>
    private static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                var accepted = ImpliedBy.TryGetValue(permission, out var implied)
                    ? implied.Append(permission).ToArray()
                    : [permission];

                options.AddPolicy(permission, policy =>
                    policy.RequireAssertion(ctx =>
                        accepted.Any(p => ctx.User.HasClaim(Permissions.ClaimType, p))));
            }
        });

        return services;
    }

    // ---------------------------------------------------------------------
    // Rate limiting
    // ---------------------------------------------------------------------

    /// <summary>
    /// محدود کردن تعداد درخواست‌ها برای جلوگیری از سوءاستفاده (ارسال انبوه پیامک OTP، حدس زدن کد یا رمز).
    /// کلید هر محدودیت آدرس IP کاربر است، یعنی هر کاربر جدا محدود می‌شود.
    /// </summary>
    private static IServiceCollection AddRequestRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddPerIpPolicy(options, "otp-request", permitLimit: 3, window: TimeSpan.FromMinutes(5));   // درخواست کد پیامکی
            AddPerIpPolicy(options, "otp-verify", permitLimit: 8, window: TimeSpan.FromMinutes(5));    // وارد کردن کد (ضد حدس)
            AddPerIpPolicy(options, "login", permitLimit: 10, window: TimeSpan.FromMinutes(15));       // ورود با رمز عبور
        });

        return services;
    }

    private static void AddPerIpPolicy(RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window) =>
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0
                }));
}
