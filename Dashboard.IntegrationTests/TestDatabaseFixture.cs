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
