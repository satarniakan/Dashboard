// Dashboard.Domain/Entities/Payment.cs
using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>تراکنش پرداخت درگاه برای یک سفارش</summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public string Gateway { get; set; } = "Zarinpal";

    /// <summary>مبلغ به تومان</summary>
    public decimal Amount { get; set; }

    /// <summary>شناسه‌ی تراکنش درگاه (Authority)</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>شماره‌ی پیگیری درگاه پس از تأیید (RefId)</summary>
    public string? RefId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Initiated;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}

public enum PaymentStatus
{
    Initiated = 1,
    Success = 2,
    Failed = 3
}
