using Microsoft.Extensions.Logging;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

// تا وقتی مشتری ثبت‌نام سامانه مودیان و انتخاب سرویس واسط (TSP) را کامل نکرده،
// این پیاده‌سازی فقط لاگ می‌کند و هیچ ارسال واقعی انجام نمی‌شود.
public class PendingElectronicInvoiceProvider : IElectronicInvoiceProvider
{
    private readonly ILogger<PendingElectronicInvoiceProvider> _logger;

    public PendingElectronicInvoiceProvider(ILogger<PendingElectronicInvoiceProvider> logger)
    {
        _logger = logger;
    }

    public Task<ElectronicInvoiceResult> SubmitInvoiceAsync(int salesInvoiceId)
    {
        _logger.LogWarning("=== ELECTRONIC INVOICE PENDING === Invoice {Id} not sent — سامانه مودیان provider not configured yet.", salesInvoiceId);
        return Task.FromResult(new ElectronicInvoiceResult(false, null, "اتصال به سامانه مودیان هنوز پیکربندی نشده است."));
    }

    public Task<string> InquiryStatusAsync(string taxUniqueId) =>
        Task.FromResult("NotConfigured");
}