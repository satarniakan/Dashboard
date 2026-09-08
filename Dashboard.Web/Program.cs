// Dashboard.Web/Program.cs
using Dashboard.Application;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Infrastructure;
using Dashboard.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Globalization;

// Configure Serilog before the host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog instead of the default logger
builder.Host.UseSerilog();

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
        policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.StockCountsManage));
// Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

// هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
await Dashboard.Infrastructure.RoleSeeder.SeedRolesAsync(app.Services);
app.UseSerilogRequestLogging();

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

// Antiforgery به هویت کاربر نیاز دارد، پس باید بعد از Authentication/Authorization بیاید
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

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
});

app.MapPost("/Account/RequestOtp", async (
    IAuthService authService,
    [FromForm] string phoneNumber) =>
{
    await authService.RequestOtpAsync(phoneNumber);
    return Results.Redirect($"/verify-otp?phone={phoneNumber}");
});

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
});

app.MapPost("/Account/CompleteProfile", async (
    HttpContext httpContext,
    IAuthService authService,
    [FromForm] string fullName,
    [FromForm] string? email,
    [FromForm] string? password,
    [FromForm] string? confirmPassword) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userId is null)
    {
        return Results.Redirect("/login");
    }

    var result = await authService.CompleteProfileAsync(userId, fullName, email, password, confirmPassword);

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
