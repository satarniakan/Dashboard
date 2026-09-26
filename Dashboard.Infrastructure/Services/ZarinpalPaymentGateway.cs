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

    public async Task<PaymentRequestResult> RequestPaymentAsync(decimal amountInToman, string description, string callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(_merchantId))
            return new PaymentRequestResult(false, null, null, "درگاه پرداخت پیکربندی نشده است (Zarinpal:MerchantId).");

        // زرین‌پال v4 مبلغ را به ریال می‌گیرد
        var amountInRial = (long)(amountInToman * 10);

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
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<ZarinpalResponse>(JsonOpts);

            if (body?.Data is not null && body.Errors is null or { Count: 0 })
            {
                var authority = body.Data.Authority;
                return new PaymentRequestResult(true, $"{StartPayUrl}{authority}", authority, null);
            }

            var error = body?.Errors is { Count: > 0 } ? $"{body.Errors[0].Code}: {body.Errors[0].Message}" : "پاسخ نامعتبر از درگاه";
            return new PaymentRequestResult(false, null, null, error);
        }
        catch (Exception ex)
        {
            return new PaymentRequestResult(false, null, null, $"خطا در ارتباط با درگاه پرداخت: {ex.Message}");
        }
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(decimal amountInToman, string authority)
    {
        var amountInRial = (long)(amountInToman * 10);

        var payload = new
        {
            merchant_id = _merchantId,
            amount = amountInRial,
            authority
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/pg/v4/payment/verify.json", payload, JsonOpts);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<ZarinpalVerifyResponse>(JsonOpts);

            if (body?.Data is not null && (body.Errors is null || body.Errors.Count == 0))
            {
                // code = 100 یعنی تأیید شد، 101 یعنی قبلاً تأیید شده (idempotent)
                if (body.Data.Code is 100 or 101)
                    return new PaymentVerificationResult(true, body.Data.RefId?.ToString(), null);
            }

            var error = body?.Errors is { Count: > 0 } ? $"{body.Errors[0].Code}: {body.Errors[0].Message}" : $"کد وضعیت: {body?.Data?.Code}";
            return new PaymentVerificationResult(false, null, error);
        }
        catch (Exception ex)
        {
            return new PaymentVerificationResult(false, null, $"خطا در تأیید پرداخت: {ex.Message}");
        }
    }

    // --- DTO های پاسخ زرین‌پال ---
    private sealed class ZarinpalResponse
    {
        [JsonPropertyName("data")] public ZarinpalRequestData? Data { get; set; }
        [JsonPropertyName("errors")] public List<ZarinpalError>? Errors { get; set; }
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
        [JsonPropertyName("errors")] public List<ZarinpalError>? Errors { get; set; }
    }

    private sealed class ZarinpalVerifyData
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("ref_id")] public long? RefId { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    private sealed class ZarinpalError
    {
        [JsonPropertyName("code")] public long Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
