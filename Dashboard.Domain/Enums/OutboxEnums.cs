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
    /// <summary>ناموفق دائمی (پس از سقف تلاش)</summary>
    Failed = 3
}
