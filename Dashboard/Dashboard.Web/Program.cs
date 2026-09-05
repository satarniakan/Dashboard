// Dashboard.Web/Program.cs
using Dashboard.Application.Services;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Dashboard.Infrastructure.Repositories;
using Dashboard.Infrastructure.Services;
using Dashboard.Web.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
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

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 3;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Required for Blazor Server to flow auth state into components
builder.Services.AddCascadingAuthenticationState();

// Application services
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IOtpRepository, OtpCodeRepository>();
builder.Services.AddScoped<ISmsSender, FakeSmsSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));
var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Auth endpoints (plain HTTP POST — required so Identity can write auth cookies)
app.MapPost("/Account/Login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] string email,
    [FromForm] string password) =>
{
    var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        return Results.Redirect("/products");
    }

    return Results.Redirect("/login?error=1");
});

app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});
app.MapPost("/Account/RequestOtp", async (
    IOtpService otpService,
    [FromForm] string phoneNumber) =>
{
    await otpService.GenerateAndSendOtpAsync(phoneNumber);
    return Results.Redirect($"/verify-otp?phone={phoneNumber}");
});

app.MapPost("/Account/VerifyOtp", async (
    IOtpService otpService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] string phoneNumber,
    [FromForm] string code) =>
{
    var isValid = await otpService.VerifyOtpAsync(phoneNumber, code);

    if (!isValid)
    {
        return Results.Redirect($"/verify-otp?phone={phoneNumber}&error=1");
    }

    var user = await userManager.FindByNameAsync(phoneNumber);
    var isNewUser = user is null;

    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = phoneNumber,
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = true
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return Results.Redirect("/register?error=1");
        }
    }

    await signInManager.SignInAsync(user, isPersistent: true);

    return isNewUser
        ? Results.Redirect("/profile?welcome=1")
        : Results.Redirect("/products");
});
app.MapPost("/Account/CompleteProfile", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    [FromForm] string fullName,
    [FromForm] string? email,
    [FromForm] string? password,
    [FromForm] string? confirmPassword) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Redirect("/login");
    }

    user.FullName = fullName;

    if (!string.IsNullOrWhiteSpace(email))
    {
        var emailResult = await userManager.SetEmailAsync(user, email);
        if (!emailResult.Succeeded)
        {
            Log.Warning("SetEmail failed: {Errors}", string.Join(" | ", emailResult.Errors.Select(e => e.Description)));
            return Results.Redirect("/profile?error=1");
        }
    }

    if (!string.IsNullOrWhiteSpace(password))
    {
        var hasPassword = await userManager.HasPasswordAsync(user);

        if (hasPassword)
        {
            return Results.Redirect("/profile?error=haspassword");
        }

        if (password != confirmPassword)
        {
            return Results.Redirect("/profile?error=mismatch");
        }

        var passwordResult = await userManager.AddPasswordAsync(user, password);
        if (!passwordResult.Succeeded)
        {
            Log.Warning("AddPassword failed: {Errors}", string.Join(" | ", passwordResult.Errors.Select(e => e.Description)));
            return Results.Redirect("/profile?error=1");
        }
    }

    await userManager.UpdateAsync(user);

    return Results.Redirect("/profile?success=1");
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