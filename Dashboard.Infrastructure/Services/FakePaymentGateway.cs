// Dashboard.Infrastructure/Services/FakePaymentGateway.cs
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

/// <summary>
/// درگاه پرداخت جعلی فقط برای تست‌های End-to-end و CI:
/// هیچ اتصال بیرونی نمی‌زند؛ RequestPayment مستقیماً به callback محلی با Status=OK ریدایرکت می‌کند
/// و VerifyPayment همیشه موفق برمی‌گردد. با «PaymentGateway:Provider=Fake» فعال می‌شود.
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    public string Name => "Fake";

    public Task<PaymentRequestResult> RequestPaymentAsync(decimal amountInToman, string description, string callbackUrl)
    {
        var authority = Guid.NewGuid().ToString("N");
        var redirectUrl = $"{callbackUrl}?Authority={authority}&Status=OK";
        return Task.FromResult(new PaymentRequestResult(true, redirectUrl, authority, null));
    }

    public Task<PaymentVerificationResult> VerifyPaymentAsync(decimal amountInToman, string authority)
    {
        if (string.IsNullOrEmpty(authority))
            return Task.FromResult(new PaymentVerificationResult(false, null, "Authority نامعتبر است."));

        return Task.FromResult(new PaymentVerificationResult(true, $"FAKE-{authority[..Math.Min(8, authority.Length)]}", null));
    }
}
