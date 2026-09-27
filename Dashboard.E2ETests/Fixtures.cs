// Dashboard.E2ETests/Fixtures.cs
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

namespace Dashboard.E2ETests;

/// <summary>
/// دیتابیس را با داده‌ی حداقلیِ ویترین آماده می‌کند (انبار فروشگاه + دو کالای منتشرشده با موجودی)
/// و کد OTP را از لاگ شبیه‌سازی پیامک می‌خواند؛ در دیتابیس فقط هشِ کد ذخیره می‌شود.
/// </summary>
public class E2EDataFixture : IAsyncLifetime
{
    private string ConnectionString
    {
        get
        {
            var raw = E2EConfig.SqlConnection ?? throw new InvalidOperationException(
                "برای اجرای تست‌های E2E متغیر TEST_MSSQL_CONNECTION (یا ConnectionStrings__Default) باید تنظیم شود.");
            // اگر catalog مشخص نشده باشد به دیتابیس پیش‌فرض SQL (master) وصل می‌شویم و جدول‌ها پیدا نمی‌شوند
            var builder = new SqlConnectionStringBuilder(raw);
            if (string.IsNullOrEmpty(builder.InitialCatalog)) builder.InitialCatalog = "DashboardDb";
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        await RunAsync(conn, $"""
            IF NOT EXISTS (SELECT 1 FROM Warehouses WHERE Id = {E2EConfig.StoreWarehouseId})
            BEGIN
                SET IDENTITY_INSERT Warehouses ON;
                INSERT INTO Warehouses (Id, Name, Code, Address, IsActive, CreatedAt)
                VALUES ({E2EConfig.StoreWarehouseId}, N'انبار فروشگاه تست', 'E2E-STORE', NULL, 1, SYSUTCDATETIME());
                SET IDENTITY_INSERT Warehouses OFF;
            END
            """);

        await UpsertProductAsync(conn, "e2e-tost-1", "کالای تست فروشگاه یک", "E2E-SKU-1", 120000m);
        await UpsertProductAsync(conn, "e2e-tost-2", "کالای تست فروشگاه دو", "E2E-SKU-2", 90000m);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task UpsertProductAsync(SqlConnection conn, string slug, string name, string sku, decimal price)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DECLARE @pid int;
            SELECT @pid = Id FROM Products WHERE Slug = @slug;
            IF @pid IS NULL
                INSERT INTO Products (Name, Slug, Sku, Price, CostPrice, Unit, ReorderPoint, IsActive, IsPublished, CreatedAt)
                VALUES (@name, @slug, @sku, @price, @price, N'عدد', 5, 1, 1, SYSUTCDATETIME());

            SELECT @pid = Id FROM Products WHERE Slug = @slug;

            IF EXISTS (SELECT 1 FROM StockLevels WHERE ProductId = @pid AND WarehouseId = @wh)
                UPDATE StockLevels SET QuantityOnHand = 50, ReservedQuantity = 0, LastUpdatedAt = SYSUTCDATETIME()
                WHERE ProductId = @pid AND WarehouseId = @wh;
            ELSE
                INSERT INTO StockLevels (ProductId, WarehouseId, QuantityOnHand, ReservedQuantity, LastUpdatedAt)
                VALUES (@pid, @wh, 50, 0, SYSUTCDATETIME());
            """;
        cmd.Parameters.AddWithValue("@slug", slug);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@wh", E2EConfig.StoreWarehouseId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// آخرین کد OTP آن شماره را از لاگ «SMS SIMULATION» بیرون می‌کشد؛ چند بار با فاصله تلاش
    /// می‌کند تا خطِ لاگ نوشته شود. مسیر لاگ با E2E_APP_LOG تعیین می‌شود (پیش‌فرض /tmp/dash-web.log).
    /// </summary>
    public async Task<string> ReadOtpCodeAsync(string phoneNumber)
    {
        var logPath = Environment.GetEnvironmentVariable("E2E_APP_LOG") ?? "/tmp/dash-web.log";
        var marker = $"To: {phoneNumber} |";

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (File.Exists(logPath))
            {
                var lines = await File.ReadAllLinesAsync(logPath);
                for (var i = lines.Length - 1; i >= 0; i--)
                {
                    if (!lines[i].Contains(marker, StringComparison.Ordinal)) continue;
                    var match = Regex.Match(lines[i], @"(?<digits>\d{6})\s*$");
                    if (match.Success) return match.Groups["digits"].Value;
                }
            }
            await Task.Delay(250);
        }
        throw new InvalidOperationException($"کد OTP برای {phoneNumber} در لاگ ({logPath}) پیدا نشد.");
    }

    private static async Task RunAsync(SqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>یک مرورگر Playwright برای کل کالکشن — هر تست Context تازه دارد (کوکی/سبد مستقل).</summary>
public class BrowserFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var options = new BrowserTypeLaunchOptions
        {
            Headless = !bool.TryParse(Environment.GetEnvironmentVariable("E2E_HEADED"), out var headed) || !headed
        };
        if (!string.IsNullOrEmpty(E2EConfig.BrowserChannel)) options.Channel = E2EConfig.BrowserChannel;
        else if (OperatingSystem.IsMacOS() && Directory.Exists("/Applications/Google Chrome.app"))
            options.Channel = "chrome";

        try
        {
            Browser = await Playwright.Chromium.LaunchAsync(options);
        }
        catch (PlaywrightException) when (options.Channel == "chrome")
        {
            // Chrome سیستم در دسترس نبود — به Chromium داخلی Playwright برگرد
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = options.Headless });
        }
    }

    public async Task<IBrowserContext> NewContextAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        context.SetDefaultTimeout(15_000);
        return context;
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2EDataFixture>, ICollectionFixture<BrowserFixture>;
