using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// هر سند انباری باید روی تراکنش‌های موجودی‌اش ReferenceId بگذارد تا تاریخچهٔ موجودی
/// قابل ردیابی تا سندِ مرجع باشد (حواله، برگشت، ضایعات، انتقال، انبارگردانی، رسید خرید).
/// </summary>
[Collection("Database")]
public class StockReferenceIdTests
{
    private readonly TestDatabaseFixture _db;

    public StockReferenceIdTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task InternalIssue_StockTransactions_PointToIssueId()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 10);

        int issueId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            issueId = await stock.RegisterInternalIssueAsync(new CreateInternalIssueDto
            {
                WarehouseId = 1,
                Purpose = "تست رفرنس",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 2 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var transactions = await context.StockTransactions
            .Where(t => t.ProductId == product.Id && t.ReferenceType == nameof(InternalIssue))
            .ToListAsync();

        Assert.NotEmpty(transactions);
        Assert.All(transactions, t => Assert.Equal(issueId, t.ReferenceId));
    }

    [SkippableFact]
    public async Task StockTransfer_BothDirections_PointToTransferId()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 10_000, costPrice: 6_000, stockQty: 10);

        int destinationWarehouseId;
        using (var scope = _db.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var warehouse = new Warehouse($"انبار مقصد {Guid.NewGuid():N}"[..20], $"WH-{Guid.NewGuid():N}"[..10]);
            dbContext.Warehouses.Add(warehouse);
            await dbContext.SaveChangesAsync();
            destinationWarehouseId = warehouse.Id;
        }

        int transferId;
        using (var scope = _db.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            transferId = await stock.RegisterStockTransferAsync(new CreateStockTransferDto
            {
                SourceWarehouseId = 1,
                DestinationWarehouseId = destinationWarehouseId,
                Notes = "تست رفرنس انتقال",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 3 } }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var transactions = await context.StockTransactions
            .Where(t => t.ProductId == product.Id && t.ReferenceType == nameof(StockTransfer))
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(transferId, t.ReferenceId));

        // موجودی مبدأ کم و مقصد زیاد شده است
        var source = await context.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        var destination = await context.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == destinationWarehouseId);
        Assert.Equal(7m, source.QuantityOnHand);
        Assert.Equal(3m, destination.QuantityOnHand);
    }
}