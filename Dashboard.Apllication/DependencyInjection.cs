using Microsoft.Extensions.DependencyInjection;
using Dashboard.Application.Services;

namespace Dashboard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IPermissionService, PermissionService>();

        // --- ماژول انبارداری (WMS) ---
        services.AddScoped<IStockService, StockService>();

        // --- ماژول فروش ---
        services.AddScoped<ISalesService, SalesService>();

        // --- ماژول حسابداری و خزانه‌داری ---
        // ترتیب ثبت در DI اهمیتی ندارد (Container خودش وابستگی‌ها را resolve می‌کند)،
        // اینجا فقط برای خوانایی، JournalService را قبل از سرویس‌هایی که به آن وابسته‌اند
        // (SalesService, TreasuryService) گذاشته‌ایم.
        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<ITreasuryService, TreasuryService>();
        services.AddScoped<IInstallmentService, InstallmentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
