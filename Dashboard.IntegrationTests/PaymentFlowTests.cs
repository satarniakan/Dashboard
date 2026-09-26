using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dashboard.IntegrationTests;

[Collection("Database")]
public class PaymentFlowTests
{
    private readonly TestDatabaseFixture _db;

    public PaymentFlowTests(TestDatabaseFixture db) => _db = db;

    [SkippableFact]
    public async Task DuplicatePaymentCallback_PaysOrderExactlyOnce()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 3);
        var authority = $"AUTH-{Guid.NewGuid():N}";
        var orderNumber = $"ORD-IT-{Guid.NewGuid():N}"[..24];

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newOrder = new Order
            {
                OrderNumber = orderNumber,
                UserId = "it-test-user",
                CustomerName = "کاربر تست یکپارچگی",
                CustomerPhone = $"0913{Random.Shared.Next(1000000, 9999999)}",
                Province = "تهران",
                City = "تهران",
                AddressLine = "خیابان تست، پلاک ۱",
                ShippingMethod = ShippingMethod.Post,
                ShippingCost = 80_000,
                Subtotal = 200_000,
                DiscountAmount = 0,
                Status = OrderStatus.PendingPayment
            };
            newOrder.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = 100_000,
                Quantity = 2
            });
            newOrder.Payments.Add(new Payment
            {
                Amount = newOrder.Total,
                Authority = authority,
                Status = PaymentStatus.Initiated
            });
            context.Orders.Add(newOrder);
            await context.SaveChangesAsync();
            orderId = newOrder.Id;
        }

        // دو callback همزمان با همان Authority — سناریوی واقعی تکرار verify درگاه
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = _db.CreateScope();
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            return await orders.MarkPaidAsync(orderId, authority, "REF-IT-1");
        }));

        Assert.All(results, r => Assert.True(r.Success, r.Error));

        using var verifyScope = _db.CreateScope();
        var context2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await context2.Orders
            .Include(o => o.Payments)
            .SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.SalesInvoiceId);

        // دقیقاً یک فاکتور فروش برای این سفارش
        Assert.Equal(1, await context2.SalesInvoices.CountAsync(i => i.Notes != null && i.Notes.Contains(orderNumber)));

        // موجودی دقیقاً یک‌بار به اندازه‌ی ۲ عدد کسر شده
        var stock = await context2.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(1m, stock.QuantityOnHand);

        // پرداخت موفق ثبت شده
        var payment = Assert.Single(order.Payments);
        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal("REF-IT-1", payment.RefId);

        // سند حسابداری متوازن است (جمع بدهکار == جمع بستانکار)
        var lines = await context2.JournalEntryLines
            .Where(l => l.JournalEntry!.ReferenceType == "store" && l.JournalEntry.ReferenceId == orderId)
            .ToListAsync();
        if (lines.Count > 0)
            Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task PaymentOnCanceledOrder_FlagsForRefund_AndNotifiesAdmin()
    {
        Skip.IfNot(_db.Available, _db.SkipReason);

        var product = await _db.SeedProductAsync(price: 100_000, costPrice: 60_000, stockQty: 5);
        var authority = $"AUTH-{Guid.NewGuid():N}";

        int orderId;
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = new Order
            {
                OrderNumber = $"ORD-IT-{Guid.NewGuid():N}"[..24],
                UserId = "it-test-user",
                CustomerName = "کاربر تست لغو",
                CustomerPhone = $"0914{Random.Shared.Next(1000000, 9999999)}",
                Province = "تهران",
                City = "تهران",
                AddressLine = "خیابان تست، پلاک ۲",
                ShippingMethod = ShippingMethod.Post,
                ShippingCost = 80_000,
                Subtotal = 100_000,
                Status = OrderStatus.Canceled // سفارش پیش از callback منقضی/لغو شده
            };
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = 100_000,
                Quantity = 1
            });
            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Authority = authority,
                Status = PaymentStatus.Initiated
            });
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        using (var scope = _db.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var (success, error) = await orders.MarkPaidAsync(orderId, authority, "REF-IT-2");

            Assert.False(success);
            Assert.Contains("بازگردانده", error);
        }

        using var verifyScope = _db.CreateScope();
        var context2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order2 = await context2.Orders.Include(o => o.Payments).SingleAsync(o => o.Id == orderId);

        // پول ثبت شده (گم نمی‌شود) و سفارش لغو‌شده می‌ماند
        Assert.Equal(OrderStatus.Canceled, order2.Status);
        Assert.Equal(PaymentStatus.Success, order2.Payments.Single().Status);
        Assert.Contains("بازگشت وجه", order2.AdminNote);

        // موجودی دست‌نخورده
        var stock = await context2.StockLevels.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == 1);
        Assert.Equal(5m, stock.QuantityOnHand);
    }
}
