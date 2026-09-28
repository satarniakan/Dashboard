using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dashboard.Infrastructure.Data;

/// <summary>
/// فقط برای `dotnet ef` در زمان طراحی — وقتی پروژهٔ استارتاپ (Web) در دسترس نیست
/// (مثلاً bin آن قفل است یا ابزار از CI صدا زده می‌شود)، همین کارخانه Context را
/// می‌سازد. کانکشن‌استرینگ فقط برای بازکردن provider است؛ `migrations add` به
/// دیتابیسِ زنده وصل نمی‌شود. در حالت عادی (با -s Dashboard.Web) تنظیمات واقعی
/// برنامه استفاده می‌شود و این کلاس نقشی ندارد.
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION")
            ?? "Server=localhost,1433;Database=DashboardDb;User Id=sa;Password=123;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new AppDbContext(options);
    }
}
