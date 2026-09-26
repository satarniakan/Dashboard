// Dashboard.Domain/Interfaces/IEmailSender.cs
namespace Dashboard.Domain.Interfaces;

/// <summary>ارسال ایمیل تراکنشی — پیاده‌سازی SMTP در Infrastructure</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
