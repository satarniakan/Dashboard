using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// مسیر کامل فروشگاه: سبد → ثبت سفارش → پرداخت موفق.
/// همان چیزی را قفل می‌کند که در اصلاحات اخیر تغییر کرد: حمل‌ونقل در فاکتور و سند،
/// ذخیرهٔ SalesInvoiceId، و رسید خودکار (در صورت پیکربندی).
/// </summary>
[Collection("Database")]
public class StorePaymentAccountingTests
{
    private readonly TestDatabaseFixture _db;

    public StorePaymentAccountingTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task PaidShopOrder_InvoiceIncludesShipping_JournalBalanced_AndReceiptPosted_WhenConfigured()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);

        int financialAccountId;
        using (var scope = _db.CreateScope())
        {
            var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
            financialAccountId = await treasury.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = $"درگاه {Guid.NewGuid():N}"[..18],
                Type = "Bank",
                BankName = "بانک تست"
            });
        }

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync("cookie-it", product.Id, 2);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-store-user", "cookie-it", new CheckoutDto
            {
                CustomerName = "کاربر تست",
                CustomerPhone = "09121234567",
                Province = "تهران",
                City = "تهران",
                AddressLine = "خیابان تست، پلاک ۱",
                ShippingMethod = ShippingMethod.Post
            });

            Assert.Null(error);
            orderId = order.Id;
        }

        string authority;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await context.Orders.SingleAsync(o => o.Id == orderId);
            authority = $"AUTH-{Guid.NewGuid():N}";
            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Authority = authority,
                Status = PaymentStatus.Initiated
            });
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var orders = new OrderService(
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                scope.ServiceProvider.GetRequiredService<ISalesService>(),
                scope.ServiceProvider.GetRequiredService<IOutboxService>(),
                scope.ServiceProvider.GetRequiredService<INotificationService>(),
                Options.Create(new StoreOptions { OnlinePaymentFinancialAccountId = financialAccountId }),
                scope.ServiceProvider.GetRequiredService<ITreasuryService>(),
                scope.ServiceProvider.GetRequiredService<ILogger<OrderService>>());

            var (success, payError) = await orders.MarkPaidAsync(orderId, authority, "REF-STORE-1");
            Assert.True(success, payError);
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var paidOrder = await vContext.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, paidOrder.Status);

        // ۱) فاکتور باید لینک شده باشد (رفع گم‌شدن SalesInvoiceId بعد از retry همزمانی)
        Assert.NotNull(paidOrder.SalesInvoiceId);
        var invoice = await vContext.SalesInvoices
            .Include(i => i.Items)
            .SingleAsync(i => i.Id == paidOrder.SalesInvoiceId);

        // ۲) مبلغ فاکتور دقیقاً برابر مبلغ پرداختی مشتری (شامل حمل‌ونقل)
        Assert.Equal(paidOrder.ShippingCost, invoice.ShippingAmount);
        Assert.Equal(paidOrder.Total, invoice.TotalAmount);

        // ۳) سند فروش: بدهکاری مشتری = مبلغ کامل + سطر درآمد حمل‌ونقل (4100)، و سند تراز
        var saleLines = await vContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesInvoice)
                     && l.JournalEntry.ReferenceId == invoice.Id)
            .ToListAsync();

        Assert.NotEmpty(saleLines);
        Assert.Equal(saleLines.Sum(l => l.DebitAmount), saleLines.Sum(l => l.CreditAmount));

        var accountIds = await vContext.Accounts
            .Where(a => a.Code == "4100" || a.Code == "1200")
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        var shippingLine = saleLines.Single(l => l.AccountId == accountIds["4100"]);
        Assert.Equal(paidOrder.ShippingCost, shippingLine.CreditAmount);

        var receivableLine = saleLines.Single(l => l.AccountId == accountIds["1200"]);
        Assert.Equal(paidOrder.Total, receivableLine.DebitAmount);

        // ۴) رسید خودکار: بدهکار صندوق/بانک، بستانکار حساب‌های دریافتنی
        var receipt = await vContext.CustomerReceipts
            .SingleAsync(r => r.FinancialAccountId == financialAccountId);
        Assert.Equal(invoice.TotalAmount, receipt.Amount);

        var receiptLines = await vContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(CustomerReceipt)
                     && l.JournalEntry.ReferenceId == receipt.Id)
            .ToListAsync();
        Assert.Equal(receiptLines.Sum(l => l.DebitAmount), receiptLines.Sum(l => l.CreditAmount));
    }
    [SkippableFact]
    public async Task PaidShopOrder_NoReceipt_WhenOnlinePaymentAccountNotConfigured()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 50_000, costPrice: 30_000, stockQty: 3);
        var cookie = $"cookie-it-{Guid.NewGuid():N}"[..24];

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync(cookie, product.Id, 1);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-no-receipt-user", cookie, new CheckoutDto
            {
                CustomerName = "کاربر تست",
                CustomerPhone = "09121234568",
                Province = "تهران",
                City = "تهران",
                AddressLine = "خیابان تست",
                ShippingMethod = ShippingMethod.Post
            });

            Assert.Null(error);
            orderId = order.Id;
        }

        string authority;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await context.Orders.SingleAsync(o => o.Id == orderId);
            authority = $"AUTH-{Guid.NewGuid():N}";
            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Authority = authority,
                Status = PaymentStatus.Initiated
            });
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await context.CustomerReceipts.CountAsync();

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (success, payError) = await orders.MarkPaidAsync(orderId, authority, "REF-STORE-2");
            Assert.True(success, payError);

            // چون صندوق پرداخت آنلاین پیکربندی نشده، رفتار قبلی حفظ می‌شود: رسیدی ثبت نمی‌شود
            Assert.Equal(before, await context.CustomerReceipts.CountAsync());
        }
    }
}