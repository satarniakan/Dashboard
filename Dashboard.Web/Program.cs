// Dashboard.Web/Program.cs
using Dashboard.Application;
using Dashboard.Infrastructure;
using Dashboard.Web;
using Dashboard.Web.Components;
using Dashboard.Web.Endpoints;
using Serilog;

// لاگر موقت («bootstrap logger») فقط برای ثبت خطاهای احتمالی هنگام بالا آمدن برنامه،
// قبل از اینکه تنظیمات اصلی از appsettings خوانده شود.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// کل مراحل راه‌اندازی (ساخت builder، DI، seed، pipeline) داخل try است تا هر خطایی
// در هر مرحله‌ای از بالا آمدن برنامه توسط Serilog ثبت شود.
try
{
    Log.Information("Starting Dashboard.Web application");

    var builder = WebApplication.CreateBuilder(args);

    // سطح لاگ‌ها از appsettings.json / appsettings.{Environment}.json خوانده می‌شود.
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

    // هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment())
        .AddWebServices(builder.Environment);

    var app = builder.Build();

    // Seed نقش‌ها، سرفصل‌های حسابداری و استان/شهرها
    await app.Services.InitializeInfrastructureAsync(app.Environment.ContentRootPath);

    // ---------- Pipeline ----------
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    // Persian culture / RTL number formatting
    app.UseRequestLocalization(options => options
        .SetDefaultCulture("fa-IR")
        .AddSupportedCultures("fa-IR")
        .AddSupportedUICultures("fa-IR"));

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();

    // درخواست‌هایی که با هیچ صفحه‌ای مطابقت ندارند به‌جای ۴۰۴ خام، به صفحه‌ی
    // طراحی‌شده‌ی not-found هدایت می‌شوند؛ مسیر اصلی در کوئری "from" می‌آید.
    app.UseStatusCodePagesWithReExecute("/not-found", "?from={0}");

    // Antiforgery به هویت کاربر نیاز دارد، پس باید بعد از Authentication/Authorization بیاید
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.UseRateLimiter();

    // ---------- Endpoints ----------
    app.MapAccountEndpoints();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException) // HostAbortedException: ابزار dotnet ef عمداً میزبان را متوقف می‌کند
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
