// Dashboard.Web/Program.cs
using Dashboard.Application;
using Dashboard.Application.Services;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Dashboard.Infrastructure;
using Dashboard.Infrastructure.Data;
using Dashboard.Web.Components;
using Dashboard.Web.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
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
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.DetailedErrors = builder.Environment.IsDevelopment());

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
    .AddPolicy(Dashboard.Domain.Identity.Permissions.StoreManage, policy =>
    policy.RequireClaim(Dashboard.Domain.Identity.Permissions.ClaimType, Dashboard.Domain.Identity.Permissions.StoreManage))
    ;

// Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

// هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

// --- فروشگاه اینترنتی: درگاه پرداخت و سرویس سفارش ---
// MerchantId/Sandbox از «Zarinpal:*»؛ انبار فروش از «Store:WarehouseId»
builder.Services.AddHttpClient("Zarinpal", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IPaymentGateway>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Zarinpal");
    return new ZarinpalPaymentGateway(
        client,
        config["Zarinpal:MerchantId"] ?? string.Empty,
        config.GetValue("Zarinpal:Sandbox", true));
});
builder.Services.Configure<StoreOptions>(builder.Configuration.GetSection(StoreOptions.SectionName));
builder.Services.AddScoped<IOrderService>(sp =>
{
    var store = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StoreOptions>>();
    return new OrderService(
        sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<ISalesService>(),
        sp.GetRequiredService<IOutboxService>(),
        sp.GetRequiredService<INotificationService>(),
        store,
        sp.GetRequiredService<ITreasuryService>(),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrderService>>());
});
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
    options.AddPolicy("order", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

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
builder.Services.AddHostedService<Dashboard.Web.Services.OrderExpiryService>();
builder.Services.AddHostedService<Dashboard.Web.Services.OutboxProcessor>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ایمیل تراکنشی — SMTP از «Email:Smtp:*»؛ Host خالی یعنی ارسال با خطای روشن در Outbox ثبت می‌شود
builder.Services.AddScoped<IEmailSender>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SmtpEmailSender(
        config["Email:Smtp:Host"] ?? string.Empty,
        config.GetValue("Email:Smtp:Port", 587),
        config["Email:Smtp:Username"],
        config["Email:Smtp:Password"],
        config["Email:Smtp:FromAddress"] ?? "no-reply@localhost",
        config["Email:Smtp:FromName"] ?? "فروشگاه");
});

// پیامک: بر اساس «Sms:Provider» — Kavenegar واقعی یا Fake (پیش‌فرض Development)
builder.Services.AddHttpClient("Sms", client => client.Timeout = TimeSpan.FromSeconds(20));
if (string.Equals(builder.Configuration["Sms:Provider"], "Kavenegar", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ISmsSender>(sp => new KavenegarSmsSender(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("Sms"),
        builder.Configuration["Sms:Kavenegar:ApiKey"] ?? string.Empty,
        builder.Configuration["Sms:Kavenegar:Sender"],
        sp.GetRequiredService<ILogger<KavenegarSmsSender>>()));
}

// ذخیره‌سازی فایل (عکس محصولات) روی دیسک، داخل wwwroot/uploads
builder.Services.AddScoped<Dashboard.Domain.Interfaces.IFileStorageService>(sp =>
    new Dashboard.Infrastructure.Services.LocalFileStorageService(
        sp.GetRequiredService<IWebHostEnvironment>().WebRootPath));
var app = builder.Build();

// Forwarded Headers — باید اولین middleware باشد تا RemoteIpAddress واقعیِ کاربر (پشت reverse proxy)
// برای Rate Limiter و لاگ در دسترس باشد؛ وگرنه همهٔ کاربران یک سطل محدودیتِ نرخ می‌شوند
// (مثلاً ۳ درخواست OTP در ۵ دقیقه برای کل سایت). پیش‌فرض فقط proxy محلی (loopback) معتبر است؛
// پشت nginx/docker/App Service باید «ForwardedHeaders:TrustAllProxies» را true کنید.
var forwardedHeadersOptions = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
};
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
{
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}
app.UseForwardedHeaders(forwardedHeadersOptions);

// اعمال خودکار مایگریشن‌های EF هنگام استارتاپ — دیگر فراموش نمی‌شوند
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
await Dashboard.Infrastructure.RoleSeeder.SeedRolesAsync(app.Services);
app.UseSerilogRequestLogging();
await Dashboard.Infrastructure.ChartOfAccountsSeeder.SeedAsync(app.Services);
await Dashboard.Infrastructure.IranLocationSeeder.SeedAsync(app.Services, app.Environment.ContentRootPath);
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

// Endpointهای احراز هویت (Dashboard.Web/Endpoints/AccountEndpoints.cs) — هر کدام فقط IAuthService را صدا می‌زنند
app.MapAccountEndpoints();
app.MapShopCartEndpoints();
app.MapNotificationsEndpoints();
app.MapShopOrderEndpoints();
app.MapSitemapEndpoints();

app.MapHealthChecks("/health");

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
