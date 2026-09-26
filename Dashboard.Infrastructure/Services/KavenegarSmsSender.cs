// Dashboard.Infrastructure/Services/KavenegarSmsSender.cs
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dashboard.Infrastructure.Services;

/// <summary>
/// ارسال پیامک واقعی از طریق کاوه‌نگار (REST).
/// تنظیمات: «Sms:Kavenegar:ApiKey» و «Sms:Kavenegar:Sender» (خط ارسال‌کننده اختیاری).
/// بدون ApiKey، ارسال با خطای روشن شکست می‌خورد تا در Outbox دیده شود.
/// </summary>
public class KavenegarSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string? _sender;
    private readonly ILogger<KavenegarSmsSender> _logger;

    public KavenegarSmsSender(HttpClient http, string apiKey, string? sender, ILogger<KavenegarSmsSender> logger)
    {
        _http = http;
        _apiKey = apiKey;
        _sender = sender;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("پیکربندی پیامک ناقص است (Sms:Kavenegar:ApiKey).");

        var payload = new Dictionary<string, string> { ["receptor"] = phoneNumber, ["message"] = message };
        if (!string.IsNullOrWhiteSpace(_sender))
            payload["sender"] = _sender;

        var response = await _http.PostAsync(
            $"https://api.kavenegar.com/v1/{_apiKey}/sms/send.json",
            new FormUrlEncodedContent(payload));

        var body = await response.Content.ReadFromJsonAsync<KavenegarResponse>();
        if (body?.Return?.Status == 200)
            return;

        _logger.LogWarning("ارسال پیامک کاوه‌نگار ناموفق: {Error}", body?.Return?.Message ?? response.StatusCode.ToString());
        throw new InvalidOperationException($"ارسال پیامک ناموفق: {body?.Return?.Message ?? response.StatusCode.ToString()}");
    }

    private sealed class KavenegarResponse
    {
        [JsonPropertyName("return")] public KavenegarReturn? Return { get; set; }
    }

    private sealed class KavenegarReturn
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
