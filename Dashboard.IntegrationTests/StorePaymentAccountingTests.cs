using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
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
            .Where(a => a.Code == SystemAccountCodes.ShippingRevenue || a.Code == SystemAccountCodes.AccountsReceivable)
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        var shippingLine = saleLines.Single(l => l.AccountId == accountIds[SystemAccountCodes.ShippingRevenue]);
        Assert.Equal(paidOrder.ShippingCost, shippingLine.CreditAmount);

        var receivableLine = saleLines.Single(l => l.AccountId == accountIds[SystemAccountCodes.AccountsReceivable]);
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

    [SkippableFact]
    public async Task Reservation_BlocksSecondBuyer_AndIsReleasedOnExpiry()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);
        const int warehouse = 1;

        // ۱) اولین خریدار رزرو می‌کند و سفارشش ساخته می‌شود
        int firstOrderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync("cookie-res-1", product.Id, 1);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-res-user-1", "cookie-res-1", new CheckoutDto
            {
                CustomerName = "خریدار اول",
                CustomerPhone = "09121230001",
                Province = "تهران",
                City = "تهران",
                AddressLine = "آدرس اول",
                ShippingMethod = ShippingMethod.Post
            });

            Assert.Null(error);
            firstOrderId = order.Id;
        }

        using (var verify = _db.CreateScope())
        {
            var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var level = await context.StockLevels
                .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse);

            // موجودی فیزیکی دست‌نخورده است، ولی رزرو شده
            Assert.Equal(1m, level.QuantityOnHand);
            Assert.Equal(1m, level.ReservedQuantity);
        }

        // ۲) خریدار دوم نباید بتواند همان کالا را بخرد
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            var added = await carts.AddToCartAsync("cookie-res-2", product.Id, 1);
            Assert.False(added.Success);
            Assert.Contains("ناموجود", added.Message ?? string.Empty);
        }

        // ۳) انقضای سفارش اول باید رزرو را آزاد کند
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await context.Orders.SingleAsync(o => o.Id == firstOrderId);
            order.CreatedAt = DateTime.UtcNow.AddDays(-2);
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            Assert.True(await orders.ExpireStalePendingOrdersAsync(TimeSpan.FromHours(6)) >= 1);
        }

        using (var verify = _db.CreateScope())
        {
            var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var level = await context.StockLevels
                .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse);
            Assert.Equal(0m, level.ReservedQuantity);

            var order = await context.Orders.SingleAsync(o => o.Id == firstOrderId);
            Assert.Equal(OrderStatus.Canceled, order.Status);
        }

        // ۴) حالا خریدار دوم می‌تواند بخرد
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            var added = await carts.AddToCartAsync("cookie-res-3", product.Id, 1);
            Assert.True(added.Success, added.Message);
        }
    }
    [SkippableFact]
    public async Task Reservation_ReleasedOnSuccessfulPayment_AndStockActuallyDecreases()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 2);

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync("cookie-res-pay", product.Id, 2);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-res-pay-user", "cookie-res-pay", new CheckoutDto
            {
                CustomerName = "خریدار پرداختی",
                CustomerPhone = "09121230003",
                Province = "تهران",
                City = "تهران",
                AddressLine = "آدرس پرداختی",
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
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (success, payError) = await orders.MarkPaidAsync(orderId, authority, "REF-RES-1");
            Assert.True(success, payError);
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);

        // موجودی فیزیکی کم شد و رزرو صفر شد
        Assert.Equal(0m, level.QuantityOnHand);
        Assert.Equal(0m, level.ReservedQuantity);
    }

    [SkippableFact]
    public async Task PaymentJustInitiated_IsNotExpired_EvenIfOrderIsOld()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 1);

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.AddToCartAsync("cookie-res-inflight", product.Id, 1);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (order, error) = await orders.PlaceOrderAsync("it-res-inflight", "cookie-res-inflight", new CheckoutDto
            {
                CustomerName = "کاربر روی درگاه",
                CustomerPhone = "09121230004",
                Province = "تهران",
                City = "تهران",
                AddressLine = "آدرس درگاه",
                ShippingMethod = ShippingMethod.Post
            });

            Assert.Null(error);
            orderId = order.Id;
        }

        // کاربر روی درگاه است: پرداخت تازه Initiated شده، ولی خودِ سفارش قدیمی است
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await context.Orders.SingleAsync(o => o.Id == orderId);
            order.CreatedAt = DateTime.UtcNow.AddDays(-2);
            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Authority = $"AUTH-{Guid.NewGuid():N}",
                Status = PaymentStatus.Initiated,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            Assert.Equal(0, await orders.ExpireStalePendingOrdersAsync(TimeSpan.FromHours(6)));
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var order2 = await vContext.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.PendingPayment, order2.Status);

        // رزرو هم باید باقی مانده باشد تا سفارش زنده بماند
        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(1m, level.ReservedQuantity);
    }

    [SkippableFact]
    public async Task CancelInvoice_AfterCostPriceChanged_StillReversesTheOriginalAmount()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        int invoiceId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری تست", "09121240001", "تهران");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id,
                WarehouseId = 1,
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 } // 200٬۰۰۰
                }
            }, "it-user");

            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        // ادمین بهای تمام‌شدهٔ کالا را بعد از صدور فاکتور عوض می‌کند (۶۰ ← ۹۰ هزار)
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await context.Products.SingleAsync(x => x.Id == product.Id);
            entity.UpdateWarehouseDetails(entity.Sku!, entity.Barcode, entity.Unit, 90_000, null, null, null, null, 0);
            await context.SaveChangesAsync();
        }

        // فاکتور قدیمی باید همان ۶۰٬۰۰۰ را برگرداند، نه ۹۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            await sales.CancelInvoiceAsync(invoiceId, "it-user");
        }

        using var verify = _db.CreateScope();
        var vContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // موجودی دقیقاً به حالت اول برگشته باشد
        var level = await vContext.StockLevels
            .SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(10m, level.QuantityOnHand);

        // و سند برگشت دقیقاً معکوس سند فروش باشد
        var entries = await vContext.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.ReferenceType == nameof(SalesInvoice) && e.ReferenceId == invoiceId)
            .ToListAsync();
        Assert.Equal(2, entries.Count);

        var accountIds = await vContext.Accounts
            .Where(a => a.Code == SystemAccountCodes.CostOfGoodsSold || a.Code == SystemAccountCodes.Inventory)
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        // سند اول = فروش (اول ساخته شده)، سند دوم = برگشت
        var ordered = entries.OrderBy(e => e.Id).ToList();
        var sale = ordered[0];
        var reversal = ordered[1];

        var saleCogs = sale.Lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.CostOfGoodsSold]).DebitAmount;
        var reversalCogs = reversal.Lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.CostOfGoodsSold]).CreditAmount;

        Assert.Equal(120_000m, saleCogs);      // ۲ × ۶۰٬۰۰۰ (بهای لحظهٔ صدور)
        Assert.Equal(saleCogs, reversalCogs);  // برگشت نباید از ۹۰٬۰۰۰ استفاده کند

        var saleInventory = sale.Lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.Inventory]).CreditAmount;
        var reversalInventory = reversal.Lines.Single(l => l.AccountId == accountIds[SystemAccountCodes.Inventory]).DebitAmount;
        Assert.Equal(saleInventory, reversalInventory);
    }
}
