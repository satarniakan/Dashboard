using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dashboard.IntegrationTests;

/// <summary>
/// مالیات بر ارزش افزوده: فروش → ۲۳۰۰ بستانکار (جزء طلب مشتری، نه درآمد)؛
/// خرید → ۱۳۵۰ بدهکار (اعتبار مالیاتی، خارج از بهای تمام‌شده)؛
/// برگشت/ابطال → سهمِ متناسبِ مالیات برمی‌گردد؛ سفارش فروشگاه → نرخ روی سفارش
/// عکس‌برداری می‌شود و فاکتورِ ساخته‌شده دقیقاً همان مبلغ درگاه را دارد.
/// </summary>
[Collection("Database")]
public class VatTests
{
    private readonly TestDatabaseFixture _db;

    public VatTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task SaleWithVat_JournalCreditsVatPayable_AndCancelReversesItFully()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        int invoiceId, customerId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری مالیاتی", "09121296001", "تهران");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId,
                WarehouseId = 1,
                ShippingAmount = 80_000,
                TaxPercent = 10m, // مبنای ۲۸۰٬۰۰۰ → مالیات ۲۸٬۰۰۰ → جمع ۳۰۸٬۰۰۰
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = 100_000 }
                }
            }, "it-user");
            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        using (var verify = _db.CreateScope())
        {
            var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await vCtx.SalesInvoices.SingleAsync(i => i.Id == invoiceId);
            Assert.Equal(28_000m, invoice.TaxAmount);
            Assert.Equal(308_000m, invoice.TotalAmount);

            var lines = await vCtx.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesInvoice)
                         && l.JournalEntry.ReferenceId == invoiceId)
                .ToListAsync();

            var vatAccountId = await vCtx.Accounts
                .Where(a => a.Code == SystemAccountCodes.VatPayable).Select(a => a.Id).SingleAsync();
            var arAccountId = await vCtx.Accounts
                .Where(a => a.Code == SystemAccountCodes.AccountsReceivable).Select(a => a.Id).SingleAsync();

            // مالیات بستانکار شده و طلب مشتری کلِ ناخالص (شامل مالیات) است
            Assert.Equal(28_000m, lines.Single(l => l.AccountId == vatAccountId).CreditAmount);
            Assert.Equal(308_000m, lines.Single(l => l.AccountId == arAccountId).DebitAmount);
            Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));

            // درآمد فروش باید خالصِ مالیات باشد (۲۰۰٬۰۰۰ نه ۲۲۸٬۰۰۰)
            var revenueAccountId = await vCtx.Accounts
                .Where(a => a.Code == SystemAccountCodes.SalesRevenue).Select(a => a.Id).SingleAsync();
            Assert.Equal(200_000m, lines.Single(l => l.AccountId == revenueAccountId).CreditAmount);
        }

        // ابطال: کل مالیات باید برگردد و ماندهٔ ۲۳۰۰ صفر شود
        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISalesService>()
                .CancelInvoiceAsync(invoiceId, "it-user");
        }

        using (var verify2 = _db.CreateScope())
        {
            var vCtx = verify2.ServiceProvider.GetRequiredService<AppDbContext>();
            var vatAccountId = await vCtx.Accounts
                .Where(a => a.Code == SystemAccountCodes.VatPayable).Select(a => a.Id).SingleAsync();
            var netVat = await vCtx.JournalEntryLines
                .Where(l => l.AccountId == vatAccountId)
                .SumAsync(l => l.DebitAmount - l.CreditAmount);
            Assert.Equal(0m, netVat);
        }
    }

    [SkippableFact]
    public async Task PartialReturn_WithVat_ReversesProportionalTaxShare()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        int invoiceId, customerId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Customer("مشتری برگشت مالیاتی", "09121297002", "تهران");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            customerId = customer.Id;

            var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
            invoiceId = await sales.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customerId,
                WarehouseId = 1,
                TaxPercent = 10m, // ۴۰۰٬۰۰۰ × ۱۰٪ = ۴۰٬۰۰۰
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 4, UnitPrice = 100_000 }
                }
            }, "it-user");
            await sales.ConfirmInvoiceAsync(invoiceId, "it-user");
        }

        // برگشت ۱ عدد از ۴: سهم مالیات = ۱۰٬۰۰۰ و کاهش طلب = ۱۱۰٬۰۰۰
        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStockService>().RegisterSalesReturnAsync(
                new CreateSalesReturnDto
                {
                    WarehouseId = 1,
                    SalesInvoiceId = invoiceId,
                    Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
                }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        var lines = await vCtx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(SalesReturn))
            .ToListAsync();

        var vatAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.VatPayable).Select(a => a.Id).SingleAsync();
        var arAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.AccountsReceivable).Select(a => a.Id).SingleAsync();

        Assert.Equal(10_000m, lines.Single(l => l.AccountId == vatAccountId).DebitAmount);
        Assert.Equal(110_000m, lines.Single(l => l.AccountId == arAccountId).CreditAmount);
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task PurchaseWithVat_DebitsVatReceivable_AndKeepsCostPriceNet()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 50_000, stockQty: 0);

        int receiptId, supplierId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier("تأمین‌کننده مالیاتی");
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;

            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
            receiptId = await stock.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = supplierId,
                WarehouseId = 1,
                TaxAmount = 50_000m, // ۵۰۰٬۰۰۰ × ۱۰٪
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitCost = 50_000 }
                }
            }, "it-user");
        }

        using var verify = _db.CreateScope();
        var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();

        // بهای میانگین خالص می‌ماند — مالیات به بهای تمام‌شده راه پیدا نمی‌کند
        var productAfter = await vCtx.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(50_000m, productAfter.CostPrice);

        var lines = await vCtx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.ReferenceType == nameof(PurchaseReceipt)
                     && l.JournalEntry.ReferenceId == receiptId)
            .ToListAsync();

        var vatAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.VatReceivable).Select(a => a.Id).SingleAsync();
        var apAccountId = await vCtx.Accounts
            .Where(a => a.Code == SystemAccountCodes.AccountsPayable).Select(a => a.Id).SingleAsync();

        // بدهکار موجودی ۵۰۰ + اعتبار مالیاتی ۵۰ / بستانکار پرداختنی ۵۵۰
        Assert.Equal(50_000m, lines.Single(l => l.AccountId == vatAccountId).DebitAmount);
        Assert.Equal(550_000m, lines.Single(l => l.AccountId == apAccountId).CreditAmount);
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task StoreOrder_WithVatRate_InvoiceMatchesGatewayAmount()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        await _db.ResetTestDataAsync();

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 10);

        var cartCookie = $"cookie-vat-{Guid.NewGuid():N}"[..22];
        int orderId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Carts.Add(new Cart
            {
                CookieId = cartCookie,
                Items = { new CartItem { ProductId = product.Id, Quantity = 2 } }
            });
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            // OrderService دستی با نرخ مالیاتی ۱۰٪ (Store:VatRate در تنظیمات واقعی صفر است)
            var sp = scope.ServiceProvider;
            var orderService = new OrderService(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<ISalesService>(),
                sp.GetRequiredService<IOutboxService>(),
                sp.GetRequiredService<INotificationService>(),
                Options.Create(new StoreOptions { WarehouseId = 1, VatRate = 10m }),
                sp.GetRequiredService<ITreasuryService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrderService>>());

            var (placedOrder, placeError) = await orderService.PlaceOrderAsync(
                "it-vat-user", cartCookie, new CheckoutDto
                {
                    CustomerName = "خریدار مالیاتی",
                    CustomerPhone = "09121298003",
                    Province = "تهران",
                    City = "تهران",
                    AddressLine = "آدرس تست",
                    ShippingMethod = ShippingMethod.Post
                });
            Assert.Null(placeError);
            orderId = placedOrder.Id;
        }

        using (var verify = _db.CreateScope())
        {
            var vCtx = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await vCtx.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId);

            // ۲۰۰٬۰۰۰ کالا + ۸۰٬۰۰۰ ارسال → مالیات ۲۸٬۰۰۰ → کل ۳۰۸٬۰۰۰
            Assert.Equal(200_000m, order.Subtotal);
            Assert.Equal(28_000m, order.TaxAmount);
            Assert.Equal(308_000m, order.Total);

            // پرداخت → فاکتور: مبلغ فاکتور باید دقیقاً برابر مبلغ پرداختی مشتری باشد
            await verify.ServiceProvider.GetRequiredService<IOrderService>()
                .AttachPaymentAsync(order.Id, "Test", order.Total, $"AUTH-VAT-{Guid.NewGuid():N}"[..20]);
            var (success, error) = await verify.ServiceProvider.GetRequiredService<IOrderService>()
                .MarkPaidAsync(order.Id, order.Payments.First().Authority!, "REF-VAT-1");
            Assert.True(success, error);

            var invoice = await vCtx.SalesInvoices.SingleAsync(i => i.Id == order.SalesInvoiceId);
            Assert.Equal(order.Total, invoice.TotalAmount);
            Assert.Equal(28_000m, invoice.TaxAmount);

            var vatAccountId = await vCtx.Accounts
                .Where(a => a.Code == SystemAccountCodes.VatPayable).Select(a => a.Id).SingleAsync();
            var vatCredit = await vCtx.JournalEntryLines
                .Where(l => l.AccountId == vatAccountId
                         && l.JournalEntry!.ReferenceType == nameof(SalesInvoice)
                         && l.JournalEntry.ReferenceId == invoice.Id)
                .SumAsync(l => l.CreditAmount);
            Assert.Equal(28_000m, vatCredit);
        }
    }
}
