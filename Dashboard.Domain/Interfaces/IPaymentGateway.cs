// Dashboard.Domain/Interfaces/IPaymentGateway.cs
namespace Dashboard.Domain.Interfaces;

/// <summary>نتیجه‌ی درخواست پرداخت از درگاه</summary>
public record PaymentRequestResult(bool Success, string? RedirectUrl, string? Authority, string? Error);

/// <summary>نتیجه‌ی تأیید پرداخت در درگاه</summary>
public record PaymentVerificationResult(bool Success, string? RefId, string? Error);

public interface IPaymentGateway
{
    /// <summary>نام درگاه (برای ثبت در رکورد Payment)</summary>
    string Name { get; }

    /// <summary>
    /// درخواست شروع پرداخت. مبلغ به تومان؛ callbackUrl آدرس بازگشت از درگاه است.
    /// </summary>
    Task<PaymentRequestResult> RequestPaymentAsync(decimal amountInToman, string description, string callbackUrl);

    /// <summary>تأیید پرداخت با Authority دریافتی از درگاه</summary>
    Task<PaymentVerificationResult> VerifyPaymentAsync(decimal amountInToman, string authority);
}
