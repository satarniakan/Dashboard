// Dashboard.Web/Services/OrderExpiryService.cs
using Dashboard.Application.Services;
using Microsoft.Extensions.Options;

namespace Dashboard.Web.Services;

/// <summary>
/// هر ۱۵ دقیقه، سفارش‌های PendingPayment قدیمی‌تر از یک ساعت را لغو می‌کند
/// تا سفارش‌های رهاشده در مرحله‌ی پرداخت، گزارش ادمین را شلوغ نکنند.
/// </summary>
public class OrderExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderExpiryService> _logger;
    private readonly StoreOptions _store;

    public OrderExpiryService(IServiceScopeFactory scopeFactory, ILogger<OrderExpiryService> logger,
        IOptions<StoreOptions> store)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _store = store.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var window = TimeSpan.FromHours(Math.Clamp(_store.OrderPaymentWindowHours, 1, 72));
                var expired = await orderService.ExpireStalePendingOrdersAsync(window);
                if (expired > 0)
                    _logger.LogInformation("سفارش‌های پرداخت‌نشده‌ی منقضی: {Count} (مهلت: {Hours:F0} ساعت)", expired, window.TotalHours);

                // بعد از انقضا: رزروهایی که هیچ سفارش پرداخت‌نشده‌ای پشتشان نیست
                // (کرش پروسه بین رزرو موجودی و درج سفارش) آزاد می‌شوند
                var orphans = await orderService.ReconcileOrphanReservationsAsync(TimeSpan.FromMinutes(10));
                if (orphans > 0)
                    _logger.LogWarning("رزرو یتیم آزادشده: {Count} کالا", orphans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در انقضای سفارش‌های پرداخت‌نشده");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
