// Dashboard.Web/Program.cs
using Dashboard.Application;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Infrastructure;
using Dashboard.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using System.Globalization;

// یک لاگر موقت («bootstrap logger») فقط برای ثبت خطاهای احتمالی هنگام بالا آمدن برنامه،
// قبل از اینکه تنظیمات اصلی از appsettings خوانده شود.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// حالا لاگر نهایی از appsettings.json / appsettings.{Environment}.json خوانده می‌شود،
// یعنی سطح لاگ‌ها بین Development و Production می‌تواند متفاوت باشد.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

// Razor Components (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Required for Blazor Server to flow auth state into components
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Dashboard.Domain.Identity.Permissions.ProductsView, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.ProductsView) ||
            ctx.User.HasClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.ProductsManage)))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.ProductsManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.ProductsManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.AuditLogsView, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.AuditLogsView))
    // --- ماژول انبارداری (WMS) ---
    // مشاهده‌ی موجودی: اگر StockView را داشته باشد یا هر کدام از مجوزهای مدیریتی زیر را، اجازه‌ی دیدن دارد
    .AddPolicy(Dashboard.Domain.Identity.Permissions.StockView, policy =>
        policy.RequireAssertion(ctx =>
        {
            var claimType = Dashboard.Domain.Identity.Permissions.ClaimType;
            return ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.StockView)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.PurchaseReceiptsManage)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.InternalIssuesManage)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.SalesReturnsManage)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.ScrapRecordsManage)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.StockTransfersManage)
                || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.StockCountsManage);
        }))

    .AddPolicy(Dashboard.Domain.Identity.Permissions.WarehousesManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.WarehousesManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.SuppliersManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.SuppliersManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.PurchaseReceiptsManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.PurchaseReceiptsManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.InternalIssuesManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.InternalIssuesManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.SalesReturnsManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.SalesReturnsManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.ScrapRecordsManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.ScrapRecordsManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.StockTransfersManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.StockTransfersManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.StockCountsManage, policy =>
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.StockCountsManage))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.CustomersManage, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.CustomersManage))
.AddPolicy(Dashboard.Domain.Identity.Permissions.SalesCreate, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.SalesCreate))
.AddPolicy(Dashboard.Domain.Identity.Permissions.SalesConfirm, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.SalesConfirm))
.AddPolicy(Dashboard.Domain.Identity.Permissions.SalesCancel, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.SalesCancel))
.AddPolicy(Dashboard.Domain.Identity.Permissions.AccountingView, policy =>
    policy.RequireAssertion(ctx =>
    {
        var claimType = Dashboard.Domain.Identity.Permissions.ClaimType;
        return ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.AccountingView)
            || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.TreasuryManage);
    }))
.AddPolicy(Dashboard.Domain.Identity.Permissions.TreasuryManage, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.TreasuryManage))
.AddPolicy(Dashboard.Domain.Identity.Permissions.SalesView, policy =>
    policy.RequireAssertion(ctx =>
    {
        var claimType = Dashboard.Domain.Identity.Permissions.ClaimType;
        return ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.SalesView)
            || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.SalesCreate)
            || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.SalesConfirm)
            || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.SalesCancel)
            || ctx.User.HasClaim(claimType, Dashboard.Domain.Identity.Permissions.CustomersManage);
    }))
    .AddPolicy(Dashboard.Domain.Identity.Permissions.CatalogManage, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.CatalogManage))
    ;

// Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

// هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// AddIdentity به‌صورت پیش‌فرض مسیر "/Account/Login" را برای صفحه‌ی ورود در نظر می‌گیرد،
// در حالی که صفحه‌ی واقعی ورود در این پروژه "/login" است. بدون این تنظیم، وقتی کاربر
// لاگ‌اوت شده باشد و بخواهد به صفحه‌ای محافظت‌شده برود، به مسیر اشتباه ریدایرکت می‌شود
// و پیام "صفحه یافت نشد" می‌بیند.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
});

// محدود کردن تعداد درخواست‌ها برای جلوگیری از سوءاستفاده: هرکس نتواند با اسکریپت
// هزاران پیامک OTP بگیرد یا کدهای ورود را حدس بزند. کلید هر محدودیت آدرس IP کاربر است،
// یعنی هر کاربر جدا محدود می‌شود، نه همه با هم.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // حداکثر ۳ درخواست کد پیامکی در هر ۵ دقیقه از هر IP
    options.AddPolicy("otp-request", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // حداکثر ۸ تلاش برای وارد کردن کد در هر ۵ دقیقه (برای جلوگیری از حدس زدن کد)
    options.AddPolicy("otp-verify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // حداکثر ۱۰ تلاش ورود با رمز عبور در هر ۱۵ دقیقه (برای جلوگیری از حدس زدن رمز)
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
});
builder.Services.AddScoped<Dashboard.Web.Services.ToastService>();
var app = builder.Build();
await Dashboard.Infrastructure.RoleSeeder.SeedRolesAsync(app.Services);
app.UseSerilogRequestLogging();
await Dashboard.Infrastructure.ChartOfAccountsSeeder.SeedAsync(app.Services);
// Persian culture / RTL number formatting
var supportedCultures = new[] { new CultureInfo("fa-IR") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("fa-IR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// درخواست‌هایی که با هیچ صفحه‌ای مطابقت ندارند (مثلاً آدرس تایپ‌شده در نوار مرورگر)
// به‌جای ۴۰۴ خام، به صفحه‌ی طراحی‌شده‌ی not-found هدایت می‌شوند.
// مسیر اصلی در کوئری "from" فرستاده می‌شود تا در آن صفحه نمایش داده شود.
app.UseStatusCodePagesWithReExecute("/not-found", "?from={0}");

// Antiforgery به هویت کاربر نیاز دارد، پس باید بعد از Authentication/Authorization بیاید
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// ===== Auth endpoints — هر کدام فقط IAuthService را صدا می‌زنند، بدون منطق تجاری =====

app.MapPost("/Account/Login", async (
    IAuthService authService,
    [FromForm] string email,
    [FromForm] string password) =>
{
    var result = await authService.LoginWithPasswordAsync(email, password);

    return result.Succeeded
        ? Results.Redirect("/")
        : Results.Redirect("/login-password?error=1");
}).RequireRateLimiting("login");

app.MapPost("/Account/RequestOtp", async (
    IAuthService authService,
    [FromForm] string phoneNumber) =>
{
    await authService.RequestOtpAsync(phoneNumber);
    return Results.Redirect($"/verify-otp?phone={phoneNumber}");
}).RequireRateLimiting("otp-request");

app.MapPost("/Account/VerifyOtp", async (
    IAuthService authService,
    [FromForm] string phoneNumber,
    [FromForm] string code) =>
{
    var result = await authService.VerifyOtpAsync(phoneNumber, code);

    if (!result.Succeeded)
    {
        return Results.Redirect($"/verify-otp?phone={phoneNumber}&error=1");
    }

    return result.IsNewUser
        ? Results.Redirect("/profile?welcome=1")
        : Results.Redirect("/");
}).RequireRateLimiting("otp-verify");

app.MapPost("/Account/CompleteProfile", async (
    HttpContext httpContext,
    IAuthService authService,
    [FromForm] string firstName,
    [FromForm] string lastName,
    [FromForm] string? email,
    [FromForm] string? province,
    [FromForm] string? city,
    [FromForm] string? address,
    [FromForm] string? password,
    [FromForm] string? confirmPassword) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userId is null)
    {
        return Results.Redirect("/login");
    }

    var result = await authService.CompleteProfileAsync(userId, firstName, lastName, email, province, city, address, password, confirmPassword);

    return result.Status switch
    {
        ProfileUpdateStatus.Success => Results.Redirect("/profile?success=1"),
        ProfileUpdateStatus.UserNotFound => Results.Redirect("/login"),
        _ => Results.Redirect($"/profile?error={result.Status}")
    };
});

app.MapPost("/logout", async (IAuthService authService) =>
{
    await authService.LogoutAsync();
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

try
{
    Log.Information("Starting Dashboard.Web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
