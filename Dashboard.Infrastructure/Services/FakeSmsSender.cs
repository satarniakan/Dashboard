// Dashboard.Infrastructure/Services/FakeSmsSender.cs
using Microsoft.Extensions.Logging;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

public class FakeSmsSender : ISmsSender
{
    private readonly ILogger<FakeSmsSender> _logger;

    public FakeSmsSender(ILogger<FakeSmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        // در نسخه واقعی، اینجا باید به API سرویس پیامک (مثل کاوه‌نگار) وصل بشه.
        _logger.LogWarning("=== SMS SIMULATION === To: {PhoneNumber} | Message: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}