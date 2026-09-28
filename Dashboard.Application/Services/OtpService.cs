using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IOtpService
{
    Task GenerateAndSendOtpAsync(string phoneNumber);
    Task<bool> VerifyOtpAsync(string phoneNumber, string code);

    /// <summary>حذف کدهای منقضی. رکوردهای منقضی هیچ‌وقت خوانده نمی‌شوند (کوئری تأیید
    /// ExpiresAt آینده می‌خواهد) و فقط جدول را پر می‌کنند. بازگشت: تعداد حذف‌شده.</summary>
    Task<int> PurgeExpiredAsync(TimeSpan grace);
}

public class OtpService : IOtpService
{
    private readonly IOtpRepository _otpRepository;
    private readonly ISmsSender _smsSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OtpService> _logger;
    private readonly IMemoryCache _attempts;
    private readonly TimeProvider _clock;

    public OtpService(IOtpRepository otpRepository, ISmsSender smsSender, IUnitOfWork unitOfWork,
        ILogger<OtpService> logger, IMemoryCache attempts, TimeProvider? clock = null)
    {
        _otpRepository = otpRepository;
        _smsSender = smsSender;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _attempts = attempts;
        // تزریق‌پذیر تا انقضای پنجرهٔ قفل در تست قابل بررسی باشد
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// سقف تلاش ناموفق برای هر شماره در بازه. محدودیت نرخِ HTTP فقط بر اساس IP است،
    /// پس بدون این شمارنده، مهاجم با چرخش IP می‌تواند یک شماره را بمباران کند
    /// (هم برای حدس کد و هم برای باطل‌کردن کدهای قبلیِ صاحبش).
    /// </summary>
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// سقف درخواست تولید کد به‌ازای هر شماره. سقف HTTP («otp-request») IP-محور است،
    /// پس مهاجم با IPهای چرخان می‌توانست یک شمارهٔ هدف را بمباران پیامکی کند:
    /// هزینهٔ مستقیم برای صاحب خط و فلج‌شدن ورودش.
    /// </summary>
    private const int MaxGenerationsPerWindow = 3;

    private static readonly TimeSpan GenerationWindow = TimeSpan.FromMinutes(15);

    /// <summary>تعداد و زمان شروع پنجره — یک رکورد، تا شمارنده پنجرهٔ لغزان نداشته باشد</summary>
    private sealed record AttemptState(int Count, DateTimeOffset WindowStart);

    private string AttemptKey(string phoneNumber) => $"otp-attempts:{phoneNumber}";

    private string GenerationKey(string phoneNumber) => $"otp-generations:{phoneNumber}";

    /// <summary>
    /// آیا پنجره پر شده؟ پنجره «لغزان» نبود: با هر رخداد، شروعِ پنجره تمدید نمی‌شود،
    /// وگرنه یک تلاش هر ۹ دقیقه شماره را برای همیشه قفل می‌کرد.
    /// </summary>
    private bool IsWindowExhausted(string key, int maxCount, TimeSpan window)
    {
        if (!_attempts.TryGetValue(key, out AttemptState? state) || state is null)
            return false;

        if (_clock.GetUtcNow() - state.WindowStart >= window)
        {
            _attempts.Remove(key); // پنجره منقضی شده
            return false;
        }

        return state.Count >= maxCount;
    }

    private void RegisterInWindow(string key, TimeSpan window)
    {
        var now = _clock.GetUtcNow();

        if (_attempts.TryGetValue(key, out AttemptState? existing) && existing is not null
            && now - existing.WindowStart < window)
        {
            // فقط شمارنده بالا می‌رود؛ شروع پنجره ثابت می‌ماند
            _attempts.Set(key, existing with { Count = existing.Count + 1 }, window);
        }
        else
        {
            _attempts.Set(key, new AttemptState(1, now), window);
        }
    }

    private bool IsBlocked(string phoneNumber)
        => IsWindowExhausted(AttemptKey(phoneNumber), MaxFailedAttempts, AttemptWindow);

    private void RegisterFailedAttempt(string phoneNumber)
        => RegisterInWindow(AttemptKey(phoneNumber), AttemptWindow);

    public async Task GenerateAndSendOtpAsync(string phoneNumber)
    {
        // شماره‌ای که چند بار پشت‌سرهم کد اشتباه داده، فعلاً ورودی جدید نمی‌گیرد
        // تا مهاجم نتواند با درخواست‌های مکرر، کدهای معتبر کاربر را باطل کند
        if (IsBlocked(phoneNumber))
        {
            _logger.LogWarning("OTP request blocked for {PhoneNumber} after repeated failures", phoneNumber);
            throw new BusinessRuleException(
                "تلاش‌های ناموفق زیاد بود. لطفاً ۱۰ دقیقه دیگر تلاش کنید.");
        }

        if (IsWindowExhausted(GenerationKey(phoneNumber), MaxGenerationsPerWindow, GenerationWindow))
        {
            _logger.LogWarning("OTP generation limit reached for {PhoneNumber}", phoneNumber);
            throw new BusinessRuleException(
                "تعداد درخواست کد برای این شماره بیش از حد مجاز است. لطفاً ۱۵ دقیقه دیگر تلاش کنید.");
        }

        // شمارش پیش از ارسال: درخواست‌هایی که ارسالشان شکست می‌خورد هم باید شمرده شوند،
        // وگرنه حلقهٔ «درخواست ← خطای سرویس پیامک ← درخواست» سقف را دور می‌زند
        RegisterInWindow(GenerationKey(phoneNumber), GenerationWindow);

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otp = new OtpCode
        {
            PhoneNumber = phoneNumber,
            // فقط هش کد ذخیره می‌شود نه خود کد — دسترسی مستقیم به دیتابیس دیگر امکان ورود نمی‌دهد
            Code = HashOtp(phoneNumber, code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        await _otpRepository.AddAsync(otp);
        await _unitOfWork.CompleteAsync();

        // پیامک پیش از باطل‌کردن کدهای قبلی ارسال می‌شود: اگر سرویس پیامک شکست بخورد،
        // کد قبلی هنوز معتبر است و کاربر نه با کدی بی‌اعتبار و نه بدون کد می‌ماند
        await _smsSender.SendAsync(phoneNumber, $"کد ورود شما: {code}");

        // فقط پس از ارسال موفق — تا آخرین کد ارسال‌شده قابل استفاده بماند
        await _otpRepository.InvalidateOthersAsync(phoneNumber, otp.Id);
        await _unitOfWork.CompleteAsync();

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

    public async Task<int> PurgeExpiredAsync(TimeSpan grace)
        => await _otpRepository.DeleteExpiredAsync(_clock.GetUtcNow().UtcDateTime - grace);

    /// <summary>
    /// هش کد یک‌بارمصرف — کد خام هرگز در دیتابیس ذخیره نمی‌شود.
    /// شمارهٔ موبایل در هش لحاظ می‌شود تا هش یک کد رایج (مثل ۱۲۳۴۵۶) برای همه یکسان نباشد؛
    /// یادآوری: فضای کد ۶ رقمی است، پس محافظت اصلی در برابر حدس، محدودیت نرخ درخواست‌هاست.
    /// </summary>
    private static string HashOtp(string phoneNumber, string code)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{phoneNumber}:{code}")));
}
