using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// چرخهٔ عمر چک: ثبت (اوراق ۱۱۴۰/۲۲۰۰) → وصول (نقد شدن، جابجایی واقعی پول)
/// یا برگشتی (بازگشت طلب/بدهی). هر اکشن باید دقیقاً یک‌بار سند بزند — دوباره‌کاری با
/// claim اتمیک وضعیت بسته می‌شود.
/// </summary>
[Collection("Database")]
public class ChequeLifecycleTests
{
    private readonly TestDatabaseFixture _db;

    public ChequeLifecycleTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task CustomerCheque_Settle_MovesMoneyFromNotesReceivableToBank_Once()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        int customerId, bankAccountId, receiptId;
        using (var scope = _db.CreateScope())
        {
            var seedCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری چک وصول", "09121294001", "تهران");
            seedCtx.Customers.Add(customer);
            await seedCtx.SaveChangesAsync();
            customerId = customer.Id;

            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            bankAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"بانک وصول {Guid.NewGuid():N}"[..18],
                Type = "Bank"
            });

            receiptId = await treasury.RegisterCustomerReceiptAsync(new CreateCustomerReceiptDto
            {
                CustomerId = customerId,
                FinancialAccountId = bankAccountId,
                Amount = 300_000,
                Method = "Cheque",
                ChequeNumber = "CH-1001",
                ChequeDueDate = DateTime.UtcNow.AddDays(10)
            }, "it-user");
        }

        // ثبت چک: پول نباید وارد بانک شده باشد — در اوراق دریافتنی است
        using (var verify0 = _db.CreateScope())
        {
            var c0 = verify0.ServiceProvider.GetRequiredService<AppDbContext>();
            var receipt0 = await c0.CustomerReceipts.SingleAsync(r => r.Id == receiptId);
            Assert.Equal(ChequeStatus.Pending, receipt0.ChequeStatus);
        }

        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            await treasury.SettleCustomerChequeAsync(receiptId,
                new SettleChequeDto { FinancialAccountId = bankAccountId }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var receipt = await context.CustomerReceipts.SingleAsync(r => r.Id == receiptId);
        Assert.Equal(ChequeStatus.Settled, receipt.ChequeStatus);

        var settlementLines = await context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == "ChequeSettlement"
                     && l.JournalEntry.ReferenceId == receiptId)
            .ToListAsync();

        // سند وصول: بدهکار حساب بانک / بستانکار اوراق دریافتنی (۱۱۴۰)
        var bankAccountId2 = await context.FinancialAccounts
            .Where(a => a.Id == bankAccountId).Select(a => a.AccountId).SingleAsync();
        var nrAccountId = await context.Accounts
            .Where(a => a.Code == SystemAccountCodes.NotesReceivable).Select(a => a.Id).SingleAsync();

        Assert.Equal(300_000m, settlementLines.Single(l => l.AccountId == bankAccountId2).DebitAmount);
        Assert.Equal(300_000m, settlementLines.Single(l => l.AccountId == nrAccountId).CreditAmount);
        Assert.Equal(settlementLines.Sum(l => l.DebitAmount), settlementLines.Sum(l => l.CreditAmount));

        // دوبار کلیک/دو کاربر همزمان: وصول دوباره باید رد شود و سند دومی زده نشود
        using (var scope2 = _db.CreateScope())
        {
            var treasury = scope2.ServiceProvider.GetRequiredService<ITreasuryService>();
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => treasury.SettleCustomerChequeAsync(
                receiptId, new SettleChequeDto { FinancialAccountId = bankAccountId }, "it-user"));
            Assert.IsType<BusinessRuleException>(ex);
        }

        var settlementCount = await context.JournalEntries
            .CountAsync(e => e.ReferenceType == "ChequeSettlement" && e.ReferenceId == receiptId);
        Assert.Equal(1, settlementCount);
    }

    [SkippableFact]
    public async Task CustomerCheque_Bounce_RestatesReceivable()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        int customerId, bankAccountId, receiptId;
        using (var scope = _db.CreateScope())
        {
            var seedCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری چک برگشتی", "09121295002", "تهران");
            seedCtx.Customers.Add(customer);
            await seedCtx.SaveChangesAsync();
            customerId = customer.Id;

            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            bankAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"بانک برگشتی {Guid.NewGuid():N}"[..18],
                Type = "Bank"
            });

            receiptId = await treasury.RegisterCustomerReceiptAsync(new CreateCustomerReceiptDto
            {
                CustomerId = customerId,
                FinancialAccountId = bankAccountId,
                Amount = 150_000,
                Method = "Cheque",
                ChequeNumber = "CH-2002",
                ChequeDueDate = DateTime.UtcNow.AddDays(-1) // سررسید گذشته و پاس نشده
            }, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            await treasury.BounceCustomerChequeAsync(receiptId,
                new BounceChequeDto { Notes = "کمبود موجودی" }, "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var receipt = await context.CustomerReceipts.SingleAsync(r => r.Id == receiptId);
        Assert.Equal(ChequeStatus.Bounced, receipt.ChequeStatus);

        var bounceLines = await context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == "ChequeBounce"
                     && l.JournalEntry.ReferenceId == receiptId)
            .ToListAsync();

        // طلب از مشتری دوباره برقرار شده (با معین) و اوراق دریافتنی خالی شده
        var arAccountId = await context.Accounts
            .Where(a => a.Code == SystemAccountCodes.AccountsReceivable).Select(a => a.Id).SingleAsync();
        var nrAccountId = await context.Accounts
            .Where(a => a.Code == SystemAccountCodes.NotesReceivable).Select(a => a.Id).SingleAsync();

        var arLine = bounceLines.Single(l => l.AccountId == arAccountId);
        Assert.Equal(150_000m, arLine.DebitAmount);
        Assert.Equal("Customer", arLine.SubsidiaryType);
        Assert.Equal(customerId, arLine.SubsidiaryId);
        Assert.Equal(150_000m, bounceLines.Single(l => l.AccountId == nrAccountId).CreditAmount);
    }

    [SkippableFact]
    public async Task SupplierCheque_SettleAndBounce_CloseTheLoop()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        int supplierId, bankAccountId, settledPaymentId, bouncedPaymentId;
        using (var scope = _db.CreateScope())
        {
            var seedCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier("تأمین‌کننده چکی");
            seedCtx.Suppliers.Add(supplier);
            await seedCtx.SaveChangesAsync();
            supplierId = supplier.Id;

            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            bankAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"بانک تأمین‌کننده {Guid.NewGuid():N}"[..18],
                Type = "Bank"
            });

            var dto = new CreateSupplierPaymentDto
            {
                SupplierId = supplierId,
                FinancialAccountId = bankAccountId,
                Amount = 400_000,
                Method = "Cheque",
                ChequeNumber = "CH-3003",
                ChequeDueDate = DateTime.UtcNow.AddDays(5)
            };
            settledPaymentId = await treasury.RegisterSupplierPaymentAsync(dto, "it-user");

            dto.ChequeNumber = "CH-3004";
            dto.ChequeDueDate = DateTime.UtcNow.AddDays(-2);
            bouncedPaymentId = await treasury.RegisterSupplierPaymentAsync(dto, "it-user");
        }

        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            // چک اول وصول می‌شود: اوراق پرداختنی تسویه و موجودی بانک واقعاً کم می‌شود
            await treasury.SettleSupplierChequeAsync(settledPaymentId,
                new SettleChequeDto { FinancialAccountId = bankAccountId }, "it-user");
            // چک دوم برمی‌گردد: بدهی به تأمین‌کننده دوباره برقرار می‌شود
            await treasury.BounceSupplierChequeAsync(bouncedPaymentId, new BounceChequeDto(), "it-user");
        }

        using var verify = _db.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var npAccountId = await context.Accounts
            .Where(a => a.Code == SystemAccountCodes.NotesPayable).Select(a => a.Id).SingleAsync();
        var apAccountId = await context.Accounts
            .Where(a => a.Code == SystemAccountCodes.AccountsPayable).Select(a => a.Id).SingleAsync();
        var bankAccountId2 = await context.FinancialAccounts
            .Where(a => a.Id == bankAccountId).Select(a => a.AccountId).SingleAsync();

        // سند وصول چک صادره: بدهکار اوراق پرداختنی / بستانکار بانک
        var settleLines = await context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == "ChequeSettlement"
                     && l.JournalEntry.ReferenceId == settledPaymentId)
            .ToListAsync();
        Assert.Equal(400_000m, settleLines.Single(l => l.AccountId == npAccountId).DebitAmount);
        Assert.Equal(400_000m, settleLines.Single(l => l.AccountId == bankAccountId2).CreditAmount);

        // سند برگشت چک صادره: بدهکار اوراق پرداختنی / بستانکار پرداختنی با معین تأمین‌کننده
        var bounceLines = await context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == "ChequeBounce"
                     && l.JournalEntry.ReferenceId == bouncedPaymentId)
            .ToListAsync();
        Assert.Equal(400_000m, bounceLines.Single(l => l.AccountId == npAccountId).DebitAmount);
        var apLine = bounceLines.Single(l => l.AccountId == apAccountId);
        Assert.Equal(400_000m, apLine.CreditAmount);
        Assert.Equal("Supplier", apLine.SubsidiaryType);
        Assert.Equal(supplierId, apLine.SubsidiaryId);

        // وضعیت‌ها
        Assert.Equal(ChequeStatus.Settled,
            (await context.SupplierPayments.SingleAsync(p => p.Id == settledPaymentId)).ChequeStatus);
        Assert.Equal(ChequeStatus.Bounced,
            (await context.SupplierPayments.SingleAsync(p => p.Id == bouncedPaymentId)).ChequeStatus);
    }
}
