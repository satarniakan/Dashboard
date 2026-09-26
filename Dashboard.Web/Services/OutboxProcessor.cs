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
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _maxAttempts;
    private readonly int _batchSize;

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
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                foreach (OutboxChannel channel in new[] { OutboxChannel.Sms, OutboxChannel.Email })
                {
                    var pending = await outboxRepo.GetPendingAsync(channel, _maxAttempts, _batchSize);
                    foreach (var message in pending)
                    {
                        message.Attempts++;
                        try
                        {
                            if (message.Channel == OutboxChannel.Email)
                                await emailSender.SendAsync(message.Recipient, message.Subject ?? "", message.Body);
                            else
                                await smsSender.SendAsync(message.Recipient, message.Body);

                            message.Status = OutboxStatus.Sent;
                            message.SentAt = DateTime.UtcNow;
                            message.LastError = null;
                            _logger.LogInformation("پیام {Channel} به {Recipient} ارسال شد", message.Channel, message.Recipient);
                        }
                        catch (Exception ex)
                        {
                            message.LastError = ex.Message;
                            if (message.Attempts >= _maxAttempts)
                            {
                                message.Status = OutboxStatus.Failed;
                                _logger.LogError(ex, "پیام {Channel} به {Recipient} پس از {Attempts} تلاش ناموفق ماند", message.Channel, message.Recipient, message.Attempts);
                            }
                            else
                            {
                                _logger.LogWarning(ex, "ارسال {Channel} به {Recipient} ناموفق (تلاش {Attempts})", message.Channel, message.Recipient, message.Attempts);
                            }
                        }
                        await outboxRepo.UpdateAsync(message);
                    }

                    if (pending.Count > 0)
                        await unitOfWork.CompleteAsync();
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
