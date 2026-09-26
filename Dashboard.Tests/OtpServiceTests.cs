using System.Text.RegularExpressions;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dashboard.Tests;

public class OtpServiceTests
{
    private readonly Mock<IOtpRepository> _otpRepository = new();
    private readonly Mock<ISmsSender> _smsSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OtpService _sut;

    public OtpServiceTests()
    {
        _unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        _sut = new OtpService(_otpRepository.Object, _smsSender.Object, _unitOfWork.Object, Mock.Of<ILogger<OtpService>>());
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_StoresHashedCode_NotPlainCode()
    {
        OtpCode? stored = null;
        string? sentMessage = null;

        _otpRepository.Setup(r => r.AddAsync(It.IsAny<OtpCode>()))
            .Callback<OtpCode>(otp => stored = otp)
            .Returns(Task.CompletedTask);
        _smsSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, message) => sentMessage = message)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAndSendOtpAsync("09121234567");

        Assert.NotNull(stored);
        Assert.NotNull(sentMessage);

        var plainCode = Regex.Match(sentMessage!, @"\d{6}").Value;
        Assert.Equal(6, plainCode.Length);

        // کد خام به مشتری پیامک می‌شود ولی هرگز در دیتابیس ذخیره نمی‌شود
        Assert.NotEqual(plainCode, stored!.Code);
        Assert.Equal(64, stored.Code.Length); // SHA-256 → 64 کاراکتر هگز
        Assert.Matches("^[0-9A-F]{64}$", stored.Code);
        Assert.DoesNotContain(plainCode, stored.Code);
    }

    [Fact]
    public async Task VerifyOtpAsync_FindsRecordByHash_Succeeds()
    {
        OtpCode? stored = null;
        string? sentMessage = null;

        _otpRepository.Setup(r => r.AddAsync(It.IsAny<OtpCode>()))
            .Callback<OtpCode>(otp => stored = otp)
            .Returns(Task.CompletedTask);
        _smsSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, message) => sentMessage = message)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAndSendOtpAsync("09121234567");
        var plainCode = Regex.Match(sentMessage!, @"\d{6}").Value;

        // جست‌وجو باید با همان هشِ ذخیره‌شده انجام شود، نه کد خام
        _otpRepository.Setup(r => r.GetLatestValidAsync("09121234567", stored!.Code)).ReturnsAsync(stored);
        _otpRepository.Setup(r => r.TryMarkAsUsedAsync(stored!.Id)).ReturnsAsync(true);

        var verified = await _sut.VerifyOtpAsync("09121234567", plainCode);

        Assert.True(verified);
        _otpRepository.Verify(r => r.GetLatestValidAsync("09121234567", It.IsAny<string>()), Times.Once);
        _otpRepository.Verify(r => r.GetLatestValidAsync("09121234567", It.Is<string>(c => c == stored!.Code)), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithWrongCode_Fails()
    {
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);

        var verified = await _sut.VerifyOtpAsync("09121234567", "000000");

        Assert.False(verified);
    }
}