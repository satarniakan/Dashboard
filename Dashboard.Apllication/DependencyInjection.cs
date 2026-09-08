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
        return services;
    }
}
