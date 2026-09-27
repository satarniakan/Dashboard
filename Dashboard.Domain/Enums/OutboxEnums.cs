// Dashboard.Domain/Enums/OutboxEnums.cs
namespace Dashboard.Domain.Enums;

public enum OutboxChannel
{
    Sms = 1,
    Email = 2
}

public enum OutboxStatus
{
    /// <summary>در انتظار ارسال</summary>
    Pending = 1,
    /// <summary>ارسال شده</summary>
    Sent = 2,
    /// <summary>نرم‌افزار /job/ پردازش آن را claim کرده و در حال ارسال است (قفل نرم)</summary>
    Processing = 4,
    /// <summary>ناموفق دائمی (پس از سقف تلاش)</summary>
    Failed = 3
}
