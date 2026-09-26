using System.Security.Cryptography;
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

    public OtpService(IOtpRepository otpRepository, ISmsSender smsSender, IUnitOfWork unitOfWork, ILogger<OtpService> logger)
    {
        _otpRepository = otpRepository;
        _smsSender = smsSender;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task GenerateAndSendOtpAsync(string phoneNumber)
    {
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
        // همان هشِ لحظهٔ ساخت اعمال می‌شود تا بدون ذخیرهٔ کد خام در دیتابیس، کد پیدا شود
        var otp = await _otpRepository.GetLatestValidAsync(phoneNumber, HashOtp(phoneNumber, code));

        if (otp is null)
        {
            _logger.LogWarning("Invalid or expired OTP attempt for {PhoneNumber}", phoneNumber);
            return false;
        }

        // مصرف اتمیک: اگر درخواست موازی دیگری قبلاً همین کد را مصرف کرده باشد، false می‌شود
        if (!await _otpRepository.TryMarkAsUsedAsync(otp.Id))
        {
            _logger.LogWarning("OTP already consumed by a concurrent request for {PhoneNumber}", phoneNumber);
            return false;
        }

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