using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Exceptions;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// انتقال بین صندوق/بانک — برای تسویهٔ پولِ پرداخت آنلاین لازم است:
/// رسید خودکار صندوقِ درگاه را بدهکار می‌کند و این سند، پول را به حساب بانکی می‌برد.
/// </summary>
[Collection("Database")]
public class AccountTransferTests
{
    private readonly TestDatabaseFixture _db;

    public AccountTransferTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task Transfer_MovesMoneyBetweenAccounts_WithBalancedJournalEntry()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        int gatewayAccountId, bankAccountId;
        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            gatewayAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"درگاه {Guid.NewGuid():N}"[..18],
                Type = "Cash"
            });
            bankAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"بانک {Guid.NewGuid():N}"[..18],
                Type = "Bank",
                BankName = "بانک تست"
            });
        }

        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            await treasury.PostAccountTransferAsync(new TransferBetweenAccountsDto
            {
                FromFinancialAccountId = gatewayAccountId,
                ToFinancialAccountId = bankAccountId,
                Amount = 500_000,
                Notes = "تسویهٔ پرداخت‌های هفته"
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var lines = await context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == "AccountTransfer")
            .ToListAsync();

        Assert.Equal(2, lines.Count);
        // سند دوطرفه باید تراز باشد
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));

        var fromCode = await context.FinancialAccounts
            .Where(a => a.Id == gatewayAccountId).Select(a => a.AccountId).SingleAsync();
        var toCode = await context.FinancialAccounts
            .Where(a => a.Id == bankAccountId).Select(a => a.AccountId).SingleAsync();

        // مبدأ بدهکار، مقصد بستانکار
        Assert.Equal(500_000m, lines.Single(l => l.AccountId == fromCode).DebitAmount);
        Assert.Equal(500_000m, lines.Single(l => l.AccountId == toCode).CreditAmount);
    }

    [SkippableFact]
    public async Task Transfer_WhenSameAccount_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        int accountId;
        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            accountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"صندوق {Guid.NewGuid():N}"[..18],
                Type = "Cash"
            });
        }

        using var scope2 = _db.CreateScope();
        var service = scope2.ServiceProvider.GetRequiredService<ITreasuryService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PostAccountTransferAsync(
            new TransferBetweenAccountsDto
            {
                FromFinancialAccountId = accountId,
                ToFinancialAccountId = accountId,
                Amount = 1000
            }, "it-user"));
    }
}
