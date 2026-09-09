using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dashboard.Domain.Identity;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Dashboard.Infrastructure.Repositories;
using Dashboard.Infrastructure.Services;

namespace Dashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 3;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
        })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOtpRepository, OtpCodeRepository>();
        services.AddScoped<ISmsSender, FakeSmsSender>();

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

        // --- ماژول حسابداری و خزانه‌داری ---
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IFinancialAccountRepository, FinancialAccountRepository>();
        services.AddScoped<ICustomerReceiptRepository, CustomerReceiptRepository>();
        services.AddScoped<ISupplierPaymentRepository, SupplierPaymentRepository>();
        services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
