using Dashboard.Application;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Infrastructure;
using Dashboard.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dashboard.IntegrationTests;

/// <summary>
/// دیتابیس SQL Server واقعی برای هر اجرای تست ساخته و در پایان حذف می‌شود.
/// رشته‌ی اتصال با متغیر محیطی TEST_MSSQL_CONNECTION قابل جایگزینی است.
/// اگر SQL Server در دسترس نباشد، تست‌ها Skip می‌شوند (نه Fail).
/// </summary>
public class TestDatabaseFixture : IAsyncLifetime
{
    private const string DefaultServerConnection =
        "Server=localhost,1433;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private readonly string _databaseName = $"DashboardIT_{Guid.NewGuid():N}"[..28];
    private ServiceProvider? _provider;

    public bool Available { get; private set; }
    public string? SkipReason { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION")
                               ?? DefaultServerConnection;

        try
        {
            var masterBuilder = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = "master" };
            await using (var master = new SqlConnection(masterBuilder.ConnectionString))
            {
                await master.OpenAsync();
                await using var cmd = master.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
                await cmd.ExecuteNonQueryAsync();
            }

            var testBuilder = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = _databaseName };
            ConnectionString = testBuilder.ConnectionString;

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Store:WarehouseId"] = "1"
            }).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(config);
            services.AddApplication();
            services.Configure<StoreOptions>(config.GetSection(StoreOptions.SectionName));
            services.AddScoped<IOrderService>(sp => new OrderService(
                sp.GetRequiredService<Domain.Interfaces.IUnitOfWork>(),
                sp.GetRequiredService<ISalesService>(),
                sp.GetRequiredService<IOutboxService>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StoreOptions>>(),
                sp.GetRequiredService<ITreasuryService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrderService>>()));

            _provider = services.BuildServiceProvider();
            Available = true;

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Database.MigrateAsync();
            }

            await ChartOfAccountsSeeder.SeedAsync(_provider);
            await SeedWarehouseAsync();
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AggregateException)
        {
            Available = false;
            SkipReason = $"SQL Server در دسترس نیست: {ex.Message}";
        }
    }

    public IServiceScope CreateScope() =>
        Available
            ? _provider!.CreateScope()
            : throw new InvalidOperationException("Fixture is not available.");

    public async Task DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();

        if (!Available) return;

        // برای اشکال‌زدایی: با KEEP_TEST_DB=1 دیتابیس حذف نمی‌شود تا بتوان اسکیمای
        // باقی‌مانده را از بیرون بررسی کرد (شمارهٔ دیتابیس در لاگ چاپ می‌شود).
        if (Environment.GetEnvironmentVariable("KEEP_TEST_DB") == "1")
        {
            Console.WriteLine($"[TestDatabaseFixture] DB نگه داشته شد: {_databaseName}");
            return;
        }

        try
        {
            var masterConnection = Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION") ?? DefaultServerConnection;
            var masterBuilder = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = "master" };
            await using var master = new SqlConnection(masterBuilder.ConnectionString);
            await master.OpenAsync();
            await using var cmd = master.CreateCommand();
            cmd.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // حذف دیتابیس تست بهترین‌تلاش است
        }
    }

    private async Task SeedWarehouseAsync()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Warehouses.Add(new Warehouse("انبار مرکزی", "WH-1"));
        await context.SaveChangesAsync();
    }

    // ----- هلپرهای seed مشترک تست‌ها -----

    /// <summary>
    /// پاک‌کردن دادهٔ تولیدشده توسط تست‌ها، به‌جز داده‌های مرجع (حساب‌های سیستمی و
    /// انبار که هر تست به آن‌ها تکیه می‌کند).
    ///
    /// چرا لازم است: دیتابیس بین کلاس‌های تست مشترک است، پس دادهٔ یک تست در تستِ
    /// دیگر دیده می‌شود. برای تست‌های آماری (مثل «فروش امروز» در داشبورد) این یعنی
    /// نتیجه به ترتیب اجرا وابسته می‌شود — تستی که تنها اجرا پاس می‌شود ولی در
    /// مجموعه شکست می‌خورد. فراخوانی این متد در ابتدای هر تست، ایزوله‌شدن را تضمین می‌کند.
    /// </summary>
    public async Task ResetTestDataAsync()
    {
        if (!Available) return;

        using var scope = CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ⚠️ روش پاک‌سازی، و دلیل هر تصمیم:
        //
        // ۱) TRUNCATE به‌جای DELETE: TRUNCATE شمارندهٔ IDENTITY را هم به صفر برمی‌گرداند.
        //    بدون آن، بعد از پاک‌سازی «انبار ۱» به انبار ۲ تبدیل می‌شود و تمام تست‌هایی
        //    که WarehouseId=1 را فرض می‌کنند (تقریباً همهٔ تست‌های موجودی) می‌شکنند.
        //
        // ۲) DROP CONSTRAINT به‌جای NOCHECK: در SQL Server حتی با NOCHECK هم TRUNCATE روی
        //    جدولی که قیدِ ارجاعی دارد رد می‌شود. تنها راه مطمئن، حذف خودِ قید است.
        //
        // ۳) بازسازی قیدها: چون DROP کردیم، باید دقیقاً همان قیدها را با همان ستون‌ها
        //    دوباره بسازیم — وگرنه اسکیمای دیتابیسِ تست خراب می‌شود و تست‌های بعدی
        //    بدون بازرسیِ صحتِ داده اجرا می‌شوند (یعنی «تست سبزِ توهمی»).
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accounts", "Warehouses",
            "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims",
            "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens",
            "__EFMigrationsHistory"
        };
        // ⚠️ هر نام جدول جداگانه داخل N'…' قرار می‌گیرد تا SQL معتبر بماند.
        // روش قدیمی (string.Join با N'…') رشتهٔ نامعتبر می‌ساخت و کل اسکریپت
        // خطا می‌داد ⇒ TRUNCATE انجام نمی‌شد و جدول‌های مرجع هم پاک می‌شدند.
        var keepInClause = string.Join(",", keep.Select(n => $"N'{n}'"));

        var script = new System.Text.StringBuilder();
        script.AppendLine("SET NOCOUNT ON;");

        // ⚠️ دو نکتهٔ حیاتی که کشف شد:
        //
        // ۱) جدول ## باید سراسری (##) باشد نه # موقت محلی: هر sp_executesql دامنهٔ
        //    تازه برای جدول‌های محلی می‌سازد، پس جدولی که در یک فراخوانی ساخته شود
        //    در فراخوانی بعدی ناپدید است.
        //
        // ۲) ستون‌های قید باید همان لحظه در جدول موقت کپی شوند: اگر بعداً از
        //    sys.foreign_key_columns بخواهیم JOIN کنیم، آن جدول در دیتابیسِ مقصد است
        //    ولی ##FKBackup در tempdb — پس JOIN هیچ ردیفی برنمی‌گرداند و
        //    بازسازی خاموش می‌ماند (FK = 0). این همان چیزی بود که دیتابیس تست را
        //    بی‌قید کرد.
        script.AppendLine("IF OBJECT_ID('tempdb..##FKBackup') IS NOT NULL DROP TABLE ##FKBackup;");
        script.AppendLine("""
            SELECT fk.name      AS ConstraintName,
                   OBJECT_NAME(fk.parent_object_id)     AS ParentTable,
                   OBJECT_NAME(fk.referenced_object_id) AS RefTable,
                   fk.parent_object_id                 AS object_id,
                   COL_NAME(fkc.parent_object_id, fkc.parent_column_id)     AS ParentCol,
                   COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS RefCol,
                   fkc.constraint_column_id            AS Ordinal
            INTO ##FKBackup
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id;
            """);

        // ⚠️ نکتهٔ کلیدی T-SQL: «SELECT @var = @var + …» مقدار قبلی را نگه نمی‌دارد،
        // بلکه هر سطر مقدار قبلی را OVERWRITE می‌کند ⇒ فقط آخرین دستور ساخته می‌شد و
        // ۶۱ قید از ۶۳ قید باقی می‌ماند. راه درست، STRING_AGG (SQL Server 2017+).
        script.AppendLine("DECLARE @sql NVARCHAR(MAX) = N'';");
        script.AppendLine("SELECT @sql = (");
        script.AppendLine("    SELECT STRING_AGG(CAST('ALTER TABLE ' AS NVARCHAR(MAX)) + QUOTENAME(ParentTable) + ' DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + ';', '')");
        script.AppendLine("    FROM (SELECT DISTINCT ParentTable, ConstraintName FROM ##FKBackup) d");
        script.AppendLine(");");
        script.AppendLine("EXEC sp_executesql @sql;");
        script.AppendLine("SET @sql = N'';");

        // خالی‌کردن جدول‌ها به‌جز مرجع
        //
        // ⚠️ ترتیب TRUNCATE حیاتی است: باید FIRST روی جدولِ «پدر» بیاید نه
        // «فرزند». مثلاً CartItems فرزندِ Carts است؛ اگر Carts زودتر خالی شود،
        // FK_CartItems_Carts_CartId می‌شکند و کل پاک‌سازی با خطا متوقف می‌شود.
        // این خطا flaky بود: STRING_AGG بدون ORDER BY ترتیبِ تصادفی می‌داد،
        // پس گاهی می‌شد و گاهی نه — یعنی تست‌های ما ناپایدار بودند.
        //
        // راه‌حل قطعی: اول بچه‌ها را خالی کن (جدولی که دیگری به آن ارجاع
        // می‌دهد)، بعد پدرها را. این کار به ترتیبِ رشته‌ای نیاز ندارد.
        script.AppendLine("SELECT @sql = (");
        script.AppendLine("    SELECT STRING_AGG(CAST('TRUNCATE TABLE ' + QUOTENAME(t.name) + ';' AS NVARCHAR(MAX)), '')");
        script.AppendLine("    FROM sys.tables t");
        script.AppendLine($"    WHERE t.is_ms_shipped = 0 AND t.name NOT IN ({keepInClause})");
        script.AppendLine("      AND EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.referenced_object_id = t.object_id)");
        script.AppendLine(");");
        script.AppendLine("EXEC sp_executesql @sql;");
        script.AppendLine("SET @sql = N'';");
        script.AppendLine("SELECT @sql = (");
        script.AppendLine("    SELECT STRING_AGG(CAST('TRUNCATE TABLE ' + QUOTENAME(t.name) + ';' AS NVARCHAR(MAX)), '')");
        script.AppendLine("    FROM sys.tables t");
        script.AppendLine($"    WHERE t.is_ms_shipped = 0 AND t.name NOT IN ({keepInClause})");
        script.AppendLine("      AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.referenced_object_id = t.object_id)");
        script.AppendLine(");");
        script.AppendLine("EXEC sp_executesql @sql;");
        script.AppendLine("SET @sql = N'';");

        // بازسازی قیدها: ستون‌های چندستونی به‌ترتیب Ordinal کنار هم میآیند
        // تا ترتیب درست ستون‌ها در قید چندستونی حفظ شود.
        script.AppendLine("SELECT @sql = (");
        script.AppendLine("""
            SELECT STRING_AGG(CAST(stmt AS NVARCHAR(MAX)), '') FROM (
                SELECT 'ALTER TABLE ' + QUOTENAME(b.ParentTable) +
                       ' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(b.ConstraintName) + ' FOREIGN KEY (' +
                       (SELECT STRING_AGG(QUOTENAME(x.ParentCol), ',') WITHIN GROUP (ORDER BY x.Ordinal)
                        FROM ##FKBackup x WHERE x.ConstraintName = b.ConstraintName) +
                       ') REFERENCES ' + QUOTENAME(b.RefTable) + '(' +
                       (SELECT STRING_AGG(QUOTENAME(y.RefCol), ',') WITHIN GROUP (ORDER BY y.Ordinal)
                        FROM ##FKBackup y WHERE y.ConstraintName = b.ConstraintName) + ');' AS stmt
                FROM (SELECT DISTINCT ConstraintName, ParentTable, RefTable FROM ##FKBackup) b
            ) s);
            """);
        script.AppendLine("EXEC sp_executesql @sql;");

        // TRUNCATE باید بیرون از تراکنش EF اجرا شود، پس مستقیم با ADO
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = script.ToString();
        await command.ExecuteNonQueryAsync();

        await ctx.SaveChangesAsync();
    }

    public async Task<Product> SeedProductAsync(decimal price, decimal costPrice, decimal stockQty, int warehouseId = 1)
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product($"SKU-{Guid.NewGuid():N}"[..20], "کالای تست", price, costPrice);
        product.SetStoreDetails(true, $"test-{Guid.NewGuid():N}"[..20], null);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        context.StockLevels.Add(new StockLevel
        {
            ProductId = product.Id,
            WarehouseId = warehouseId,
            QuantityOnHand = stockQty,
            LastUpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return product;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
}
