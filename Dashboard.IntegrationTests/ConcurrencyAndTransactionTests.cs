using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dashboard.IntegrationTests;

[Collection("Database")]
public class ConcurrencyAndTransactionTests
{
    private readonly TestDatabaseFixture _db;

    public ConcurrencyAndTransactionTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task ExecuteInTransaction_CommitsAndRollsBack_OnRealSqlServer()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        using var scope = _db.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // تراکنش روی SQL Server واقعی با EnableRetryOnFailure کار می‌کند (رگرسیون باگ بحرانی:
        // قبلاً هر کوئری داخل تراکنش دستی InvalidOperationException می‌گرفت)
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.AuditLogs.AddAsync(new AuditLog("IT-Commit", null, "commit test"));
            await uow.CompleteAsync();
        });

        // در صورت استثنا کل تراکنش رول‌بک می‌شود
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.AuditLogs.AddAsync(new AuditLog("IT-Rollback", null, "rollback test"));
            await uow.CompleteAsync();
            throw new InvalidOperationException("boom");
        }));

        // فراخوانی تو‌در‌تو در همان تراکنش بیرونی ادغام می‌شود
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.ExecuteInTransactionAsync(async () =>
            {
                await uow.AuditLogs.AddAsync(new AuditLog("IT-Nested", null, "nested test"));
                await uow.CompleteAsync();
            });
        });

        using var verifyScope = _db.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await context.AuditLogs.AnyAsync(a => a.EventType == "IT-Commit"));
        Assert.False(await context.AuditLogs.AnyAsync(a => a.EventType == "IT-Rollback"));
        Assert.True(await context.AuditLogs.AnyAsync(a => a.EventType == "IT-Nested"));
    }

    [SkippableFact]
    public async Task CompleteAsync_RowVersionConflict_ThrowsDbUpdateConcurrencyException()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 1000, costPrice: 500, stockQty: 10);

        // دو scope مستقل، هر دو یک رکورد موجودی را می‌خوانند
        using var scopeA = _db.CreateScope();
        using var scopeB = _db.CreateScope();
        var uowA = scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var uowB = scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uowA.StockLevels.GetAsync(product.Id, 1);
        await uowB.StockLevels.GetAsync(product.Id, 1);

        await uowA.StockLevels.IncreaseOrCreateAsync(product.Id, 1, +1);
        await uowA.CompleteAsync();

        // scopeB حالا RowVersion کهنه دارد — باید DbUpdateConcurrencyException بگیرد
        // (نه BusinessRuleException؛ رگرسیون باگ بلعیدن استثنا در CompleteAsync)
        await uowB.StockLevels.IncreaseOrCreateAsync(product.Id, 1, +1);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => uowB.CompleteAsync());
    }

    [SkippableFact]
    public async Task ConcurrentSaleOfLastItem_OnlyOneSucceeds_StockNeverNegative()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 50_000, costPrice: 30_000, stockQty: 1);

        // دو فاکتور پیش‌نویس، هر کدام ۱ عدد از آخرین موجودی
        var invoiceIds = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            using var scope = _db.CreateScope();
            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            var customer = await sales.CreateCustomerAsync(new CreateCustomerDto
            {
                Name = $"مشتری موازی {i}-{Guid.NewGuid():N}"[..30],
                Phone = $"0912000{i:D5}"
            });
            invoiceIds.Add(await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id,
                WarehouseId = 1,
                Items = [new SalesInvoiceItemInput { ProductId = product.Id, Quantity = 1, UnitPrice = 50_000 }]
            }, "it-test"));
        }

        // تأیید همزمان — دقیقاً یکی باید موفق شود
        var results = await Task.WhenAll(invoiceIds.Select(async id =>
        {
            using var scope = _db.CreateScope();
            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            try
            {
                await sales.ConfirmInvoiceAsync(id, "it-test");
                return true;
            }
            catch (Exception ex) when (ex is Domain.Exceptions.BusinessRuleException or DbUpdateConcurrencyException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(r => r));

        using var verifyScope = _db.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stock = await context.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(0m, stock.QuantityOnHand); // هرگز منفی نمی‌شود
    }

    [SkippableFact]
    public async Task ConcurrentDiscountConsumption_RespectsUsageCap()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        int discountId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var code = new DiscountCode
            {
                Code = $"ITCAP{Guid.NewGuid():N}"[..15],
                Type = Domain.Enums.DiscountType.Percentage,
                Value = 10,
                MaxUsageCount = 5,
                UsageCount = 0,
                IsActive = true
            };
            context.DiscountCodes.Add(code);
            await context.SaveChangesAsync();
            discountId = code.Id;
        }

        // ۲۰ تلاش موازی برای مصرف کدی با سقف ۵
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = _db.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await uow.DiscountCodes.TryConsumeUsageAsync(discountId);
        }));

        Assert.Equal(5, results.Count(r => r));

        using var verifyScope = _db.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await verifyContext.DiscountCodes.Where(d => d.Id == discountId).Select(d => d.UsageCount).SingleAsync());
    }
}
