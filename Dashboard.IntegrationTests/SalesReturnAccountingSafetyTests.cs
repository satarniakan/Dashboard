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
/// مرحلهٔ ۱ بازبینی: قوانین بنیادی سند حسابداری.
/// <see cref="Dashboard.Application.Services.JournalService"/> روی هر عملیات مالی سیستم
/// (خرید، فروش، برگشت، انتقال، دریافت) صدا زده می‌شود؛ اگر این قوانین درست نباشد،
/// هیچ گزارش مالیِ دیگری قابل اعتماد نیست.
/// </summary>
[Collection("Database")]
public class JournalIntegrityTests
{
    private readonly TestDatabaseFixture _db;
    public JournalIntegrityTests(TestDatabaseFixture db) => _db = db;

    private static decimal DebitTotal(AppDbContext ctx, int entryId) =>
        ctx.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntryId == entryId).Sum(l => l.DebitAmount);

    private static decimal CreditTotal(AppDbContext ctx, int entryId) =>
        ctx.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntryId == entryId).Sum(l => l.CreditAmount);

    /// <summary>ترازی سند باید در دیتابیس هم حفظ شود، نه فقط در حافظه.</summary>
    [SkippableFact]
    public async Task PostEntry_BalancedEntry_IsPersistedBalanced()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        int entryId;
        using (var scope = _db.CreateScope())
        {
            var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();
            entryId = await journal.PostEntryAsync(
                "سند تست متعادل",
                new List<JournalLineInput>
                {
                    new(SystemAccountCodes.Inventory, 500_000m, 0m, "خرید کالا"),
                    new(SystemAccountCodes.AccountsPayable, 0m, 500_000m, "بدهی به فروشنده")
                });
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(DebitTotal(vCtx, entryId), CreditTotal(vCtx, entryId));
        Assert.Equal(500_000m, DebitTotal(vCtx, entryId));
    }

    /// <summary>سند نامتوازن نباید اصلاً ثبت شود.</summary>
    [SkippableFact]
    public async Task PostEntry_UnbalancedEntry_Throws_AndPersistsNothing()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        int before;
        using (var scope = _db.CreateScope())
        {
            var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();
            before = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .JournalEntries.AsNoTracking().CountAsync();

            await Assert.ThrowsAsync<BusinessRuleException>(() => journal.PostEntryAsync(
                "سند نامتوازن",
                new List<JournalLineInput>
                {
                    new(SystemAccountCodes.Inventory, 100_000m, 0m),
                    new(SystemAccountCodes.AccountsPayable, 0m, 90_000m)
                }));
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(before, await vCtx.JournalEntries.AsNoTracking().CountAsync());
    }

    /// <summary>کمتر از دو سطر، سند حسابداری نیست.</summary>
    [SkippableFact]
    public async Task PostEntry_SingleLine_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => journal.PostEntryAsync(
            "تک‌سطری",
            new List<JournalLineInput> { new(SystemAccountCodes.Inventory, 100_000m, 0m) }));
    }

    /// <summary>
    /// شکاف واقعی: سرویس فقط «مجموع بدهکار = مجموع بستانکار» را چک می‌کند.
    /// یک سطر که «هم‌زمان» بدهکار و بستانکار دارد، تراز کلی را خنثی می‌کند و سیستم
    /// آن را می‌پذیرد — ولی در دفتر کل بی‌معناست (یک مبلغ هم به حساب بدهکار و هم بستانکار شد).
    /// </summary>
    [SkippableFact]
    public async Task PostEntry_LineWithBothDebitAndCredit_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();

        // جمع بدهکار = 100,000 و جمع بستانکار = 100,000 ⇒ تراز است، ولی سطر اول
        // ۵۰٬۰۰۰ بدهکار و ۵۰٬۰۰۰ بستانکار دارد که در حسابداری معتبر نیست.
        await Assert.ThrowsAsync<BusinessRuleException>(() => journal.PostEntryAsync(
            "سطر دوحالته",
            new List<JournalLineInput>
            {
                new(SystemAccountCodes.Inventory, 50_000m, 50_000m, "هم بدهکار هم بستانکار"),
                new(SystemAccountCodes.AccountsPayable, 50_000m, 100_000m)
            }));
    }

    /// <summary>مبلغ منفی نباید در سند پذیرفته شود.</summary>
    [SkippableFact]
    public async Task PostEntry_NegativeAmount_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => journal.PostEntryAsync(
            "سند با مبلغ منفی",
            new List<JournalLineInput>
            {
                new(SystemAccountCodes.Inventory, -100_000m, 0m),
                new(SystemAccountCodes.AccountsPayable, 0m, -100_000m)
            }));
    }

    /// <summary>کد حساب ناموجود نباید ساخته شود (وگرنه گزارش‌ها بی‌ردّ می‌مانند).</summary>
    [SkippableFact]
    public async Task PostEntry_UnknownAccountCode_Throws()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        using var scope = _db.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IJournalService>();

        await Assert.ThrowsAsync<NotFoundException>(() => journal.PostEntryAsync(
            "کد حساب ناموجود",
            new List<JournalLineInput>
            {
                new("9999", 100_000m, 0m),
                new(SystemAccountCodes.AccountsPayable, 0m, 100_000m)
            }));
    }
}

/// <summary>
/// ایمنی مالیِ برگشت از فروش و لغو فاکتور — سناریوهایی که می‌توانند
/// درآمد/موجودی را چندبار برگردانند و دفتر کل را «تراز ولی غلط» کنند.
/// </summary>
[Collection("Database")]
public class SalesReturnAccountingSafetyTests
{
    private readonly TestDatabaseFixture _db;
    public SalesReturnAccountingSafetyTests(TestDatabaseFixture db) => _db = db;

    /// <summary>
    /// اثر خالص سند فروش و لغوِ آن روی درآمد: فروش ۴۰۰٬۰۰۰، لغو باید فقط
    /// باقیمانده (۳ عدد = ۳۰۰٬۰۰۰) را برگرداند ⇒ خالص ۱۰۰٬۰۰۰.
    /// سندِ خودِ برگشت جداگانه و با مرجع SalesReturn ثبت می‌شود و اینجا نمی‌آید.
    /// </summary>
    private static decimal SalesInvoiceRevenueNet(AppDbContext ctx, int invoiceId)
    {
        var revenueAccountId = ctx.Accounts.AsNoTracking().Single(a => a.Code == SystemAccountCodes.SalesRevenue).Id;
        var lines = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesInvoice)
                     && l.JournalEntry.ReferenceId == invoiceId
                     && l.AccountId == revenueAccountId)
            .ToList();
        return lines.Sum(l => l.CreditAmount) - lines.Sum(l => l.DebitAmount);
    }

    /// <summary>
    /// مجموعِ درآمدی که از این فاکتور کسر شده: سند لغو (مرجع SalesInvoice) + سند برگشت (مرجع SalesReturn).
    /// باید دقیقاً برابر درآمد فروش اولیه باشد تا نه کمتر (سیستم بیش از فروش برگشته) و نه
    /// بیشتر (بخشی از فروش در حسابداری باقی مانده) باشد.
    /// </summary>
    private static decimal TotalReversedRevenue(AppDbContext ctx, int invoiceId)
    {
        var revenueAccountId = ctx.Accounts.AsNoTracking().Single(a => a.Code == SystemAccountCodes.SalesRevenue).Id;
        var returnIds = ctx.SalesReturns.AsNoTracking().Where(r => r.SalesInvoiceId == invoiceId).Select(r => r.Id).ToList();

        var cancelDebit = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesInvoice)
                     && l.JournalEntry.ReferenceId == invoiceId
                     && l.AccountId == revenueAccountId)
            .Sum(l => l.DebitAmount);

        var returnsDebit = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesReturn)
                     && returnIds.Contains(l.JournalEntry.ReferenceId!.Value)
                     && l.AccountId == revenueAccountId)
            .Sum(l => l.DebitAmount);

        return cancelDebit + returnsDebit;
    }

    /// <summary>مجموع اثر برگشت‌های ثبت‌شده (مرجع SalesReturn) روی درآمد.</summary>
    private static decimal ReturnsRevenueTotal(AppDbContext ctx, int invoiceId)
    {
        var revenueAccountId = ctx.Accounts.AsNoTracking().Single(a => a.Code == SystemAccountCodes.SalesRevenue).Id;
        var returnIds = ctx.SalesReturns.AsNoTracking().Where(r => r.SalesInvoiceId == invoiceId).Select(r => r.Id).ToList();
        return ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesReturn)
                     && returnIds.Contains(l.JournalEntry.ReferenceId!.Value)
                     && l.AccountId == revenueAccountId)
            .Sum(l => l.DebitAmount);
    }

    /// <summary>
    /// رگرسیون: یک کالا با دو سطر و «مقدارهای نامساوی» در همان فاکتور فروش.
    /// لغو فاکتور باید درآمد را به‌اندازهٔ جمع واقعی سطرها برگرداند
    /// (۳۰×۱۰٬۰۰۰ + ۱۰×۲۰٬۰۰۰ = ۵۰۰٬۰۰۰). استفاده از Average ساده به‌جای
    /// میانگین وزنی، درآمد را ۴۰×۱۵٬۰۰۰ = ۶۰۰٬۰۰۰ می‌کرد.
    /// </summary>
    [SkippableFact]
    public async Task CancelInvoice_SameProductUnevenQuantities_ReversesActualRevenue()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 50);

        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست وزنی", "09121292002", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 30, UnitPrice = 10_000 },
                    new() { ProductId = product.Id, Quantity = 10, UnitPrice = 20_000 }
                }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");
        }

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISalesService>().CancelInvoiceAsync(invoiceId, "u");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // فروش ۵۰۰٬۰۰۰ ⇒ پس از لغو کامل، خالص درآمد فروش این فاکتور باید صفر باشد
        Assert.Equal(0m, SalesInvoiceRevenueNet(vCtx, invoiceId));
    }

    /// <summary>
    /// ادعای باگ: اعتبارسنجی «مقدار &gt; ۰» فقط داخل شاخهٔ «با فاکتور مرجع» اجرا می‌شد.
    /// مرجوعیِ بدون فاکتور هیچ بررسی‌ای نداشت ⇒ مقدار منفی یک «موجودی» منفی و یک
    /// موجب تراکنشِ منفی می‌ساخت (یعنی به‌جای برگشت، موجودی را کم می‌کرد).
    /// </summary>
    [SkippableFact]
    public async Task SalesReturn_WithoutReferenceInvoice_RejectsNonPositiveQuantity()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        using var scope = _db.CreateScope();
        var stock = scope.ServiceProvider.GetRequiredService<IStockService>();

        // مرجوعی بدون فاکتور مرجع و با مقدار منفی ⇒ باید رد شود
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => stock.RegisterSalesReturnAsync(
            new CreateSalesReturnDto
            {
                WarehouseId = 1, SalesInvoiceId = null,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = -100 } }
            }, "u"));

        // باید «خطای کسب‌وکار» باشد (نه خطای فنی دیتابیس) — یعنی اعتبارسنجی خودمان کار کرده
        Assert.IsType<BusinessRuleException>(ex);

        // موجودی نباید تغییری کرده باشد
        using var check = _db.CreateScope();
        var cCtx = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var lvl = await cCtx.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(10m, lvl.QuantityOnHand);

        // هیچ تراکنش موجودیِ «برگشت فروش» برای این کالا ثبت نشده باشد
        Assert.False(await cCtx.StockTransactions.AsNoTracking()
            .AnyAsync(t => t.ProductId == product.Id && t.Type == StockTransactionType.SalesReturn));
    }

    /// <summary>
    /// رگرسیون: برگشت وقتی فاکتور مرجع برای یک کالا «دو سطر» دارد. ساخت ToDictionary
    /// روی ProductId با کلید تکراری خطا می‌داد؛ ضمناً درآمدِ برگشتی باید
    /// ۲۰×۱۰٬۰۰۰ + ۱۰×۲۰٬۰۰۰ = ۴۰۰٬۰۰۰ باشد (میانگین وزنی، نه ساده).
    /// </summary>
    [SkippableFact]
    public async Task SalesReturn_InvoiceWithSameProductTwice_UsesWeightedPrice_AndDoesNotThrow()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 100);

        int invoiceId, customerId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست برگشت وزنی", "09121293003", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 20, UnitPrice = 10_000 },
                    new() { ProductId = product.Id, Quantity = 10, UnitPrice = 20_000 }
                }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");
        }

        // برگشت کل مقدار فروخته‌شده (۳۰ عدد) — نباید خطای کلید تکراری بدهد
        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStockService>().RegisterSalesReturnAsync(
                new CreateSalesReturnDto
                {
                    WarehouseId = 1, SalesInvoiceId = invoiceId,
                    Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 30 } }
                }, "u");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // فروش ۴۰۰٬۰۰۰ و برگشت کامل ⇒ هیچ درآمدی باقی نمانده باشد
        Assert.Equal(0m, TotalReversedRevenue(vCtx, invoiceId) - 400_000m);
    }

    [SkippableFact]
    public async Task PartialReturn_ThenCancelInvoice_ReversesOnlyTheRemaining()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        int invoiceId, customerId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست ایمنی", "09121291001", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 4, UnitPrice = 100_000 } }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");
        }

        // برگشت ۱ عدد ⇒ ۱۰۰٬۰۰۰ کاهش درآمد (سند مستقل خودش را دارد)
        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStockService>().RegisterSalesReturnAsync(
                new CreateSalesReturnDto
                {
                    WarehouseId = 1, SalesInvoiceId = invoiceId,
                    Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
                }, "u");
        }

        // لغو کل فاکتور ⇒ فقط ۳ عدد باقیمانده (۳۰۰٬۰۰۰) باید برگردد
        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISalesService>().CancelInvoiceAsync(invoiceId, "u");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // فروش ۴ عدد = ۴۰۰٬۰۰۰ درآمد
        //   • ۱ عدد برگشت شد  ⇒ سند مستقل ۱۰۰٬۰۰۰
        //   • لغو فاکتور      ⇒ فقط ۳ عددِ باقیمانده = ۳۰۰٬۰۰۰ برمی‌گردد
        // پس خالصِ باقیماندهٔ فاکتور = ۴۰۰ − ۳۰۰ = ۱۰۰ (یعنی فقط همان ۱ عددِ برگشتی باقی مانده)
        Assert.Equal(100_000m, SalesInvoiceRevenueNet(vCtx, invoiceId));
        Assert.Equal(100_000m, ReturnsRevenueTotal(vCtx, invoiceId));

        // مجموعِ کلِ درآمدِ فروشِ این فاکتور که از بیرون کسر شده = ۳۰۰ (لغو) + ۱۰۰ (برگشت) = ۴۰۰
        Assert.Equal(400_000m, TotalReversedRevenue(vCtx, invoiceId));

        // موجودی هم باید دقیقاً به ۱۰ برگردد (نه ۹ یا ۱۱)
        var level = await vCtx.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(10m, level.QuantityOnHand);
    }

    [SkippableFact]
    public async Task PartialReturn_ThenFullCancel_LeavesJournalBalanced()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست تراز", "09121291002", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 } }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");

            await scope.ServiceProvider.GetRequiredService<IStockService>().RegisterSalesReturnAsync(
                new CreateSalesReturnDto
                {
                    WarehouseId = 1, SalesInvoiceId = invoiceId,
                    Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
                }, "u");
        }

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISalesService>().CancelInvoiceAsync(invoiceId, "u");
        }

        using var v = _db.CreateScope();
        var vCtx = v.ServiceProvider.GetRequiredService<AppDbContext>();

        // هر سندِ مرتبط با این فاکتور باید تراز باشد
        var entryIds = await vCtx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesInvoice) && l.JournalEntry.ReferenceId == invoiceId)
            .Select(l => l.JournalEntryId).Distinct().ToListAsync();

        foreach (var entryId in entryIds)
        {
            var lines = await vCtx.JournalEntryLines.AsNoTracking()
                .Where(l => l.JournalEntryId == entryId).ToListAsync();
            Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
        }

        // و موجودی کل روی این فاکتور به حالت اول برگشته باشد
        var level = await vCtx.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(5m, level.QuantityOnHand);
    }

    [SkippableFact]
    public async Task Return_AgainstDraftInvoice_IsRejected()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست پیش‌نویس", "09121291003", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 1, UnitPrice = 100_000 } }
            }, "u"); // تأیید نشده
        }

        using var scope2 = _db.CreateScope();
        var stock = scope2.ServiceProvider.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => stock.RegisterSalesReturnAsync(
            new CreateSalesReturnDto
            {
                WarehouseId = 1, SalesInvoiceId = invoiceId,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
            }, "u"));
    }

    [SkippableFact]
    public async Task Return_ExceedingAlreadyReturned_IsRejected()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);
        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست تجمعی", "09121291004", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 } }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");

            // ۲ عدد فروخته شده، ۱ عدد برگشته ⇒ فقط ۱ عدد قابل برگشت است
            await scope.ServiceProvider.GetRequiredService<IStockService>().RegisterSalesReturnAsync(
                new CreateSalesReturnDto
                {
                    WarehouseId = 1, SalesInvoiceId = invoiceId,
                    Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
                }, "u");
        }

        using var scope2 = _db.CreateScope();
        var stock = scope2.ServiceProvider.GetRequiredService<IStockService>();

        // برگشت ۲ عدد دیگر ⇒ باید رد شود (چون فقط ۱ عدد باقی مانده)
        await Assert.ThrowsAsync<BusinessRuleException>(() => stock.RegisterSalesReturnAsync(
            new CreateSalesReturnDto
            {
                WarehouseId = 1, SalesInvoiceId = invoiceId,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 2 } }
            }, "u"));
    }

    [SkippableFact]
    public async Task Return_WithZeroOrNegativeQuantity_IsRejected()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        // هر تست از دادهٔ خالی شروع می‌شود تا به دادهٔ تست‌های دیگر وابسته نباشد
        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("تست صفر", "09121291005", "تهران");
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id, WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                { new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 } }
            }, "u");
            await sales.ConfirmInvoiceAsync(invoiceId, "u");
        }

        using var scope2 = _db.CreateScope();
        var stock = scope2.ServiceProvider.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => stock.RegisterSalesReturnAsync(
            new CreateSalesReturnDto
            {
                WarehouseId = 1, SalesInvoiceId = invoiceId,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 0 } }
            }, "u"));
    }
}
