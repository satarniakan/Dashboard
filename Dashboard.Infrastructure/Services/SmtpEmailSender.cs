// Dashboard.Infrastructure/Services/SmtpEmailSender.cs
using System.Net;
using System.Net.Mail;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

/// <summary>
/// ارسال ایمیل با SMTP (بدون وابستگی به پکیج بیرونی).
/// تنظیمات: «Email:Smtp:Host/Port/Username/Password/FromAddress/FromName» — Host خالی یعنی پیکربندی نشده.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public SmtpEmailSender(string host, int port, string? username, string? password, string fromAddress, string fromName)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _fromAddress = fromAddress;
        _fromName = fromName;
    }

    public Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_host))
            throw new InvalidOperationException("پیکربندی ایمیل ناقص است (Email:Smtp:Host).");

        var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(to);

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(_username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_username, _password)
        };

        return client.SendMailAsync(message);
    }
}
