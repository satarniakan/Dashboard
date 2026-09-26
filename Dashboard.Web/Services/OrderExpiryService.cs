// Dashboard.Web/Services/OrderExpiryService.cs
using Dashboard.Application.Services;

namespace Dashboard.Web.Services;

/// <summary>
/// هر ۱۵ دقیقه، سفارش‌های PendingPayment قدیمی‌تر از یک ساعت را لغو می‌کند
/// تا سفارش‌های رهاشده در مرحله‌ی پرداخت، گزارش ادمین را شلوغ نکنند.
/// </summary>
public class OrderExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderExpiryService> _logger;

    public OrderExpiryService(IServiceScopeFactory scopeFactory, ILogger<OrderExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var expired = await orderService.ExpireStalePendingOrdersAsync(TimeSpan.FromHours(1));
                if (expired > 0)
                    _logger.LogInformation("سفارش‌های پرداخت‌نشده‌ی منقضی: {Count}", expired);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در انقضای سفارش‌های پرداخت‌نشده");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
