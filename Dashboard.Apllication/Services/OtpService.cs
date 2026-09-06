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
        var code = Random.Shared.Next(100000, 999999).ToString();

        var otp = new OtpCode
        {
            PhoneNumber = phoneNumber,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(2),
            IsUsed = false
        };

        await _otpRepository.AddAsync(otp);
        await _unitOfWork.CompleteAsync();
        await _smsSender.SendAsync(phoneNumber, $"کد ورود شما: {code}");

        _logger.LogInformation("OTP generated for {PhoneNumber}", phoneNumber);
    }

    public async Task<bool> VerifyOtpAsync(string phoneNumber, string code)
    {
        var otp = await _otpRepository.GetLatestValidAsync(phoneNumber, code);

        if (otp is null)
        {
            _logger.LogWarning("Invalid or expired OTP attempt for {PhoneNumber}", phoneNumber);
            return false;
        }

        await _otpRepository.MarkAsUsedAsync(otp.Id);
        await _unitOfWork.CompleteAsync();  
        return true;
    }
}