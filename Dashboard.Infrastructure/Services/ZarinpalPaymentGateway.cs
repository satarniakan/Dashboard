// Dashboard.Infrastructure/Services/ZarinpalPaymentGateway.cs
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

/// <summary>
/// درگاه پرداخت زرین‌پال (v4 REST). MerchantId از تنظیمات «Zarinpal:MerchantId» خوانده می‌شود؛
/// اگر خالی باشد به‌صورت sandbox و بدون مرچنت واقعی کار نمی‌کند و خطای روشن می‌دهد.
/// برای محیط واقعی «Zarinpal:Sandbox» را false کنید.
/// </summary>
public class ZarinpalPaymentGateway : IPaymentGateway
{
    public string Name => "Zarinpal";

    private readonly HttpClient _http;
    private readonly string _merchantId;
    private readonly bool _sandbox;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ZarinpalPaymentGateway(HttpClient http, string merchantId, bool sandbox)
    {
        _http = http;
        _merchantId = merchantId;
        _sandbox = sandbox;
    }

    private string BaseUrl => _sandbox ? "https://sandbox.zarinpal.com" : "https://payment.zarinpal.com";
    private string StartPayUrl => $"{BaseUrl}/StartPay/";

    // گرد کردن (نه truncate) به ریال: مبلغ request و verify باید دقیقاً یکسان باشند،
    // وگرنه تراکنشِ پرداخت‌شده در مرحله‌ی verify به‌خاطر اختلاف چند ریالی رد می‌شود
    private static long ToRial(decimal amountInToman) =>
        (long)Math.Round(amountInToman * 10m, 0, MidpointRounding.AwayFromZero);

    public async Task<PaymentRequestResult> RequestPaymentAsync(decimal amountInToman, string description, string callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(_merchantId))
            return new PaymentRequestResult(false, null, null, "درگاه پرداخت پیکربندی نشده است (Zarinpal:MerchantId).");

        // زرین‌پال v4 مبلغ را به ریال می‌گیرد
        var amountInRial = ToRial(amountInToman);

        var payload = new
        {
            merchant_id = _merchantId,
            amount = amountInRial,
            description,
            callback_url = callbackUrl
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/pg/v4/payment/request.json", payload, JsonOpts);

            // در v4 خطاها با HTTP 4xx و بدنه‌ی JSON برمی‌گردند — اول بدنه خوانده می‌شود
            // تا علت واقعی (مرچنت نامعتبر، مبلغ کمتر از کف و…) به کاربر برسد
            var body = await response.Content.ReadFromJsonAsync<ZarinpalResponse>(JsonOpts);

            if (response.IsSuccessStatusCode && body?.Errors is null && body?.Data is not null)
            {
                // code = 100 یعنی درخواست پرداخت با موفقیت ساخته شد
                if (body.Data.Code == 100 && !string.IsNullOrEmpty(body.Data.Authority))
                    return new PaymentRequestResult(true, $"{StartPayUrl}{body.Data.Authority}", body.Data.Authority, null);

                return new PaymentRequestResult(false, null, null,
                    $"درگاه درخواست پرداخت را نپذیرفت (کد {body.Data.Code}: {body.Data.Message ?? "بدون توضیح"}).");
            }

            var error = FormatError(body?.Errors) ?? $"درگاه با وضعیت HTTP {(int)response.StatusCode} پاسخ داد.";
            return new PaymentRequestResult(false, null, null, error);
        }
        catch (Exception ex)
        {
            return new PaymentRequestResult(false, null, null, $"خطا در ارتباط با درگاه پرداخت: {ex.Message}");
        }
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(decimal amountInToman, string authority)
    {
        if (string.IsNullOrWhiteSpace(_merchantId))
            return new PaymentVerificationResult(false, null, "درگاه پرداخت پیکربندی نشده است (Zarinpal:MerchantId).");

        var amountInRial = ToRial(amountInToman);

        var payload = new
        {
            merchant_id = _merchantId,
            amount = amountInRial,
            authority
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/pg/v4/payment/verify.json", payload, JsonOpts);
            var body = await response.Content.ReadFromJsonAsync<ZarinpalVerifyResponse>(JsonOpts);

            if (response.IsSuccessStatusCode && body?.Errors is null && body?.Data is not null)
            {
                // code = 100 یعنی تأیید شد، 101 یعنی قبلاً تأیید شده (idempotent)
                if (body.Data.Code is 100 or 101)
                    return new PaymentVerificationResult(true, body.Data.RefId?.ToString(), null);

                return new PaymentVerificationResult(false, null,
                    $"تأیید پرداخت ناموفق (کد {body.Data.Code}: {body.Data.Message ?? "بدون توضیح"}).");
            }

            var error = FormatError(body?.Errors) ?? $"کد وضعیت: {body?.Data?.Code}";
            return new PaymentVerificationResult(false, null, error);
        }
        catch (Exception ex)
        {
            return new PaymentVerificationResult(false, null, $"خطا در تأیید پرداخت: {ex.Message}");
        }
    }

    private static string? FormatError(ZarinpalErrorBody? errors) =>
        errors is null ? null : $"خطای درگاه (کد {errors.Code}): {errors.Message ?? "بدون توضیح"}";

    // --- DTO های پاسخ زرین‌پال ---
    // در v4 فیلد errors یک «آبجکت» است (نه آرایه): { code, message, validations }
    private sealed class ZarinpalResponse
    {
        [JsonPropertyName("data")] public ZarinpalRequestData? Data { get; set; }
        [JsonPropertyName("errors")] public ZarinpalErrorBody? Errors { get; set; }
    }

    private sealed class ZarinpalRequestData
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("authority")] public string Authority { get; set; } = string.Empty;
        [JsonPropertyName("fee")] public long Fee { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    private sealed class ZarinpalVerifyResponse
    {
        [JsonPropertyName("data")] public ZarinpalVerifyData? Data { get; set; }
        [JsonPropertyName("errors")] public ZarinpalErrorBody? Errors { get; set; }
    }

    private sealed class ZarinpalVerifyData
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("ref_id")] public long? RefId { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    private sealed class ZarinpalErrorBody
    {
        [JsonPropertyName("code")] public long Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
