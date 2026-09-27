// Dashboard.E2ETests/E2EConfig.cs
namespace Dashboard.E2ETests;

/// <summary>تنظیمات اجرای تست‌های E2E — همه از متغیرهای محیطی خوانده می‌شوند.</summary>
public static class E2EConfig
{
    public static string BaseUrl { get; } =
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5293";

    /// <summary>اتصال به SQL Server برای seed داده و خواندن کد OTP (همان convention تست‌های یکپارچگی).</summary>
    public static string? SqlConnection { get; } =
        Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

    /// <summary>شماره‌ی مشتری تستی (کاربر در ورود OTP خودکار ساخته می‌شود).</summary>
    public const string CustomerPhone = "09301112233";

    /// <summary>انبار فروشگاه — باید با Store:WarehouseId اپلیکیشن هم‌خوان باشد.</summary>
    public const int StoreWarehouseId = 2002;

    /// <summary>در محیط CI با playwright install chromium اجرا می‌شود؛ محلی می‌توان از Chrome سیستم استفاده کرد.</summary>
    public static string? BrowserChannel { get; } = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER_CHANNEL");
}
