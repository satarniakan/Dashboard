// Dashboard.Web/Services/OutboxProcessor.cs
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Dashboard.Web.Services;

/// <summary>
/// هر ۳۰ ثانیه صف پیام‌ها (Outbox) را می‌گیرد و بر اساس کانال با ISmsSender یا
/// IEmailSender می‌فرستد. تلاش ناموفق تا سقف مشخص دوباره تلاش می‌شود؛
/// بعد از سقف، Failed دائمی ثبت می‌گردد.
/// ارسال «دست‌کم‌به‌یک» است: هر پیام پیش از ارسال با یک UPDATE شرطی claim می‌شود
/// تا دو instance (یا دو دور هم‌پوشان) یکی را دوباره نفرستند.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _maxAttempts;
    private readonly int _batchSize;

    /// <summary>
    /// عمر مجاز یک پیام در وضعیت Processing. بیشتر از این، «یتیم» فرض می‌شود (کرش پروسه)
    /// و به صف برمی‌گردد. باید از بلندترین ارسال ممکن (timeout سرویس پیامک/ایمیل) بزرگ‌تر باشد،
    /// وگرنه در استقرار چندنمونه‌ای پیامِ در-حال‌ارسالِ instance دیگر پس از مهلت دوباره ارسال می‌شود.
    /// </summary>
    private static readonly TimeSpan SendLease = TimeSpan.FromMinutes(10);

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxAttempts = configuration.GetValue("Sms:MaxAttempts", 3);
        _batchSize = configuration.GetValue("Sms:BatchSize", 20);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                // فقط رکوردهایی که از مهلتِ ارسال هم گذشته‌اند یتیم‌اند؛ رکورد Processingِ تازه
                // یعنی instance دیگری همین حالا آن را می‌فرستد و دست‌زدن به آن = ارسال تکراری
                var reclaimed = await outboxRepo.ReclaimAbandonedAsync(SendLease);
                if (reclaimed > 0)
                    _logger.LogWarning("{Count} پیام مانده در Processing به صف برگشت", reclaimed);

                foreach (OutboxChannel channel in new[] { OutboxChannel.Sms, OutboxChannel.Email })
                {
                    var pending = await outboxRepo.GetPendingAsync(channel, _maxAttempts, _batchSize);
                    foreach (var message in pending)
                    {
                        // یک زمان واحد برای claim و نتیجه: «شروع قفل» و «پایان ارسال»
                        var claimedAt = DateTime.UtcNow;

                        if (!await outboxRepo.TryClaimForSendingAsync(message.Id, _maxAttempts, claimedAt))
                            continue; // دور/instance دیگری زودتر آن را برداشته

                        var attempts = message.Attempts + 1;
                        string? error = null;
                        try
                        {
                            if (message.Channel == OutboxChannel.Email)
                                await emailSender.SendAsync(message.Recipient, message.Subject ?? "", message.Body);
                            else
                                await smsSender.SendAsync(message.Recipient, message.Body);
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }

                        await outboxRepo.FinishSendingAsync(message.Id, error is null, attempts, _maxAttempts,
                            error, DateTime.UtcNow);

                        if (error is null)
                        {
                            _logger.LogInformation("پیام {Channel} به {Recipient} ارسال شد", message.Channel, message.Recipient);
                        }
                        else if (attempts >= _maxAttempts)
                        {
                            _logger.LogError("پیام {Channel} به {Recipient} پس از {Attempts} تلاش ناموفق ماند: {Error}",
                                message.Channel, message.Recipient, attempts, error);
                        }
                        else
                        {
                            _logger.LogWarning("ارسال {Channel} به {Recipient} ناموفق (تلاش {Attempts}): {Error}",
                                message.Channel, message.Recipient, attempts, error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در پردازش صف پیام‌ها");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
