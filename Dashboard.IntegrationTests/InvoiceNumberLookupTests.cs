using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// کوئری «شماره فاکتور» نباید وضعیت را فیلتر کند: فاکتور ملغی «پیدا نشد» گزارش می‌شد
/// و کاربر همان شماره را دوباره امتحان می‌کرد. قاعدهٔ «برگشت فقط روی تأییدشده»
/// در StockService نگهبانی شده و آن‌جا هم تست دارد.
/// </summary>
[Collection("Database")]
public class InvoiceNumberLookupTests
{
    private readonly TestDatabaseFixture _db;

    public InvoiceNumberLookupTests(TestDatabaseFixture db) => _db = db;

    private async Task<string> SeedInvoiceAsync(SalesInvoiceStatus status)
    {
        using var scope = _db.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var number = $"L-{Guid.NewGuid():N}"[..12];
        ctx.SalesInvoices.Add(new SalesInvoice
        {
            InvoiceNumber = number,
            Status = status,
            // انبار مرجع ۱ در ResetTestDataAsync دست‌نخورده می‌ماند
            WarehouseId = 1,
            TotalAmount = 250_000m
        });
        await ctx.SaveChangesAsync();
        return number;
    }

    [SkippableFact]
    public async Task CanceledInvoice_IsFoundByNumber_WithItsStatus()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        var number = await SeedInvoiceAsync(SalesInvoiceStatus.Canceled);

        using var scope = _db.CreateScope();
        var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
        var found = await sales.GetInvoiceByNumberAsync(number);

        Assert.NotNull(found);
        Assert.Equal(number, found!.InvoiceNumber);
        // UI با همین رشته پیام «این فاکتور ملغی است» را می‌سازد، نه «پیدا نشد»
        Assert.Equal(nameof(SalesInvoiceStatus.Canceled), found.Status);
    }

    [SkippableFact]
    public async Task UnknownNumber_StillReturnsNull()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();

        Assert.Null(await sales.GetInvoiceByNumberAsync($"NOPE-{Guid.NewGuid():N}"));
    }
}
