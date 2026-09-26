using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Dashboard.Infrastructure.Repositories;
using Dashboard.Infrastructure.Services;

namespace Dashboard.Infrastructure;

public static class DependencyInjection
{
    /// <param name="isDevelopment">
    /// پیش‌فرض false (امن‌ترین حالت). فقط در Development متن پیامک‌های شبیه‌سازی‌شده (شامل کد OTP) لاگ می‌شود.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 3;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
        })
     .AddEntityFrameworkStores<AppDbContext>()
     .AddDefaultTokenProviders()
     .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

        // AddIdentity به‌صورت پیش‌فرض مسیر "/Account/Login" را برای صفحه‌ی ورود در نظر می‌گیرد،
        // در حالی که صفحه‌ی واقعی ورود در این پروژه "/login" است. بدون این تنظیم، کاربر لاگ‌اوت‌شده
        // که به صفحه‌ی محافظت‌شده برود به مسیر اشتباه ریدایرکت می‌شود و «صفحه یافت نشد» می‌بیند.
        // (باید بعد از AddIdentity بیاید.)
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
        });

        // --- عمومی / احراز هویت ---
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOtpRepository, OtpCodeRepository>();
        services.AddScoped<ISmsSender>(sp =>
            new FakeSmsSender(sp.GetRequiredService<ILogger<FakeSmsSender>>(), logMessageBody: isDevelopment));
        services.AddScoped<ILocationRepository, LocationRepository>();

        // --- محصولات و کاتالوگ ---
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();

        // --- ماژول انبارداری (WMS) ---
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IStockLevelRepository, StockLevelRepository>();
        services.AddScoped<IStockTransactionRepository, StockTransactionRepository>();
        services.AddScoped<IPurchaseReceiptRepository, PurchaseReceiptRepository>();
        services.AddScoped<IInternalIssueRepository, InternalIssueRepository>();
        services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
        services.AddScoped<IScrapRecordRepository, ScrapRecordRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<IStockCountRepository, StockCountRepository>();

        // --- ماژول فروش ---
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
        services.AddScoped<IElectronicInvoiceProvider, PendingElectronicInvoiceProvider>();

        // --- ماژول حسابداری و خزانه‌داری ---
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IFinancialAccountRepository, FinancialAccountRepository>();
        services.AddScoped<ICustomerReceiptRepository, CustomerReceiptRepository>();
        services.AddScoped<ISupplierPaymentRepository, SupplierPaymentRepository>();
        services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();

        return services;
    }

    /// <summary>
    /// داده‌های پایه‌ی لازم برای اجرای برنامه را (در صورت نبودن) می‌سازد.
    /// باید بعد از app.Build() و قبل از app.Run() صدا زده شود. ترتیب اجرا مهم است.
    /// </summary>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, string contentRootPath)
    {
        await RoleSeeder.SeedRolesAsync(services);
        await ChartOfAccountsSeeder.SeedAsync(services);
        await IranLocationSeeder.SeedAsync(services, contentRootPath);
    }
}
