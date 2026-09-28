// Dashboard.Web/Services/OtpCleanupService.cs
using Dashboard.Application.Services;

namespace Dashboard.Web.Services;

/// <summary>
/// هر ۶ ساعت، رکوردهای کد یک‌بارمصرفِ منقضی را پاک می‌کند. این رکوردها هیچ‌وقت
/// خوانده نمی‌شوند (کوئری تأیید فقط ExpiresAt آینده را می‌گیرد) ولی با هر ورود
/// پیامکی یکی ساخته می‌شوند، پس بدون پاک‌سازی جدول بی‌حد رشد می‌کند.
/// یک ساعت دست‌خورده نگه داشته می‌شوند تا بررسیِ «چرا کد من کار نکرد» ممکن بماند.
/// </summary>
public class OtpCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtpCleanupService> _logger;

    public OtpCleanupService(IServiceScopeFactory scopeFactory, ILogger<OtpCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // راه‌اندازی سرویس پیام‌رسان/دیتابیس در لحظهٔ استارتاپ سنگین است؛ اولین دور را عقب می‌اندازیم
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // خطای یک دور نباید سرویس را بکشد: دور بعدی دوباره تلاش می‌کند
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();
                    var deleted = await otpService.PurgeExpiredAsync(Grace);
                    if (deleted > 0)
                        _logger.LogInformation("کدهای منقضیِ OTP حذف‌شده: {Count}", deleted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پاک‌سازی کدهای OTP");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // توقف عادی اپلیکیشن
        }
    }
}
