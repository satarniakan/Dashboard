using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(OtpCode otp);
    Task<OtpCode?> GetLatestValidAsync(string phoneNumber, string code);
    Task MarkAsUsedAsync(int id);

    /// <summary>مصرف اتمیک و یک‌بارمصرف: فقط اگر IsUsed=false باشد علامت می‌خورد —
    /// دو درخواست موازی با یک کد هر دو موفق نمی‌شوند</summary>
    Task<bool> TryMarkAsUsedAsync(int id);

    /// <summary>باطل‌کردن کدهای معتبر قبلی همان شماره — فقط آخرین کد ارسال‌شده معتبر بماند</summary>
    Task InvalidatePreviousAsync(string phoneNumber);
}