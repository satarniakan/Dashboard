using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IOtpService
{
    Task GenerateAndSendOtpAsync(string phoneNumber);
    Task<bool> VerifyOtpAsync(string phoneNumber, string code);
}

public class OtpService : IOtpService
{
    private readonly IOtpRepository _otpRepository;
    private readonly ISmsSender _smsSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OtpService> _logger;
    private readonly IMemoryCache _attempts;

    public OtpService(IOtpRepository otpRepository, ISmsSender smsSender, IUnitOfWork unitOfWork,
        ILogger<OtpService> logger, IMemoryCache attempts)
    {
        _otpRepository = otpRepository;
        _smsSender = smsSender;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _attempts = attempts;
    }

    /// <summary>
    /// سقف تلاش ناموفق برای هر شماره در بازه. محدودیت نرخِ HTTP فقط بر اساس IP است،
    /// پس بدون این شمارنده، مهاجم با چرخش IP می‌تواند یک شماره را بمباران کند
    /// (هم برای حدس کد و هم برای باطل‌کردن کدهای قبلیِ صاحبش).
    /// </summary>
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    private string AttemptKey(string phoneNumber) => $"otp-attempts:{phoneNumber}";

    private bool IsBlocked(string phoneNumber) =>
        _attempts.TryGetValue(AttemptKey(phoneNumber), out int count) && count >= MaxFailedAttempts;

    private void RegisterFailedAttempt(string phoneNumber)
    {
        var key = AttemptKey(phoneNumber);
        var count = _attempts.TryGetValue(key, out int current) ? current : 0;
        _attempts.Set(key, count + 1, AttemptWindow);
    }

    public async Task GenerateAndSendOtpAsync(string phoneNumber)
    {
        // شماره‌ای که چند بار پشت‌سرهم کد اشتباه داده، فعلاً ورودی جدید نمی‌گیرد
        // تا مهاجم نتواند با درخواست‌های مکرر، کدهای معتبر کاربر را باطل کند
        if (IsBlocked(phoneNumber))
        {
            _logger.LogWarning("OTP request blocked for {PhoneNumber} after repeated failures", phoneNumber);
            return;
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otp = new OtpCode
        {
            PhoneNumber = phoneNumber,
            // فقط هش کد ذخیره می‌شود نه خود کد — دسترسی مستقیم به دیتابیس دیگر امکان ورود نمی‌دهد
            Code = HashOtp(phoneNumber, code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        // کدهای معتبر قبلی باطل می‌شوند — فقط آخرین کد ارسال‌شده قابل استفاده است
        await _otpRepository.InvalidatePreviousAsync(phoneNumber);
        await _otpRepository.AddAsync(otp);
        await _unitOfWork.CompleteAsync();
        await _smsSender.SendAsync(phoneNumber, $"کد ورود شما: {code}");

        _logger.LogInformation("OTP generated for {PhoneNumber}", phoneNumber);
    }

    public async Task<bool> VerifyOtpAsync(string phoneNumber, string code)
    {
        if (IsBlocked(phoneNumber))
        {
            _logger.LogWarning("OTP verification blocked for {PhoneNumber} after repeated failures", phoneNumber);
            return false;
        }

        // همان هشِ لحظهٔ ساخت اعمال می‌شود تا بدون ذخیرهٔ کد خام در دیتابیس، کد پیدا شود
        var otp = await _otpRepository.GetLatestValidAsync(phoneNumber, HashOtp(phoneNumber, code));

        if (otp is null)
        {
            RegisterFailedAttempt(phoneNumber);
            _logger.LogWarning("Invalid or expired OTP attempt for {PhoneNumber}", phoneNumber);
            return false;
        }

        // مصرف اتمیک: اگر درخواست موازی دیگری قبلاً همین کد را مصرف کرده باشد، false می‌شود
        if (!await _otpRepository.TryMarkAsUsedAsync(otp.Id))
        {
            _logger.LogWarning("OTP already consumed by a concurrent request for {PhoneNumber}", phoneNumber);
            return false;
        }

        // ورود موفق ⇒ شمارندهٔ تلاش‌های ناموفق پاک می‌شود
        _attempts.Remove(AttemptKey(phoneNumber));

        await _unitOfWork.CompleteAsync();
        return true;
    }

    /// <summary>
    /// هش کد یک‌بارمصرف — کد خام هرگز در دیتابیس ذخیره نمی‌شود.
    /// شمارهٔ موبایل در هش لحاظ می‌شود تا هش یک کد رایج (مثل ۱۲۳۴۵۶) برای همه یکسان نباشد؛
    /// یادآوری: فضای کد ۶ رقمی است، پس محافظت اصلی در برابر حدس، محدودیت نرخ درخواست‌هاست.
    /// </summary>
    private static string HashOtp(string phoneNumber, string code)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{phoneNumber}:{code}")));
}