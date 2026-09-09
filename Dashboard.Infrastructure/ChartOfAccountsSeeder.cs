using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure;

/// <summary>
/// سرفصل‌های پایه‌ی حسابداری را در اولین اجرا می‌سازد. این سرفصل‌ها «سیستمی» هستند
/// (IsSystemAccount = true) چون کد از روی کدشان (نه Id) به آن‌ها ارجاع می‌دهد — پس
/// نباید از داخل UI قابل حذف باشند.
/// </summary>
public static class ChartOfAccountsSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var defaults = new (string Code, string Name, AccountType Type)[]
        {
            (SystemAccountCodes.Cash, "صندوق", AccountType.Asset),
            (SystemAccountCodes.AccountsReceivable, "حساب‌های دریافتنی (مشتریان)", AccountType.Asset),
            (SystemAccountCodes.Inventory, "موجودی کالا", AccountType.Asset),
            (SystemAccountCodes.AccountsPayable, "حساب‌های پرداختنی (تأمین‌کنندگان)", AccountType.Liability),
            (SystemAccountCodes.SalesRevenue, "فروش کالا", AccountType.Revenue),
            (SystemAccountCodes.CostOfGoodsSold, "بهای تمام‌شده کالای فروش‌رفته", AccountType.Expense),
        };

        foreach (var (code, name, type) in defaults)
        {
            var exists = await context.Accounts.AnyAsync(a => a.Code == code);
            if (!exists)
            {
                context.Accounts.Add(new Account
                {
                    Code = code,
                    Name = name,
                    Type = type,
                    IsSystemAccount = true
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
