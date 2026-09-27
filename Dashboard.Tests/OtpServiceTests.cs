using System.Text.RegularExpressions;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
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
        _sut = new OtpService(_otpRepository.Object, _smsSender.Object, _unitOfWork.Object,
            Mock.Of<ILogger<OtpService>>(), new MemoryCache(new MemoryCacheOptions()));
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

    [Fact]
    public async Task VerifyOtpAsync_AfterFiveWrongCodes_BlocksFurtherAttempts()
    {
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);

        for (var i = 0; i < 5; i++)
            Assert.False(await _sut.VerifyOtpAsync("09121230000", "000000"));

        // تلاش ششم حتی با کد درست هم رد می‌شود (محدودیت بر اساس شماره است، نه IP)
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OtpCode { Id = 1, PhoneNumber = "09121230000" });
        Assert.False(await _sut.VerifyOtpAsync("09121230000", "111111"));
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_AfterFiveWrongCodes_ThrowsBusinessRule()
    {
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);
        for (var i = 0; i < 5; i++)
            await _sut.VerifyOtpAsync("09121230001", "000000");

        // مهاجم نباید بتواند با درخواست‌های مکرر، کد معتبر کاربر را باطل کند؛
        // و کاربر هم باید پیام روشن بگیرد (نه بازگشت بی‌صدا)
        _otpRepository.Setup(r => r.AddAsync(It.IsAny<OtpCode>()));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.GenerateAndSendOtpAsync("09121230001"));

        _otpRepository.Verify(r => r.AddAsync(It.IsAny<OtpCode>()), Times.Never);
        _smsSender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_SuccessAfterFailures_ClearsTheCounter()
    {
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);
        for (var i = 0; i < 4; i++)
            Assert.False(await _sut.VerifyOtpAsync("09121230002", "000000"));

        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OtpCode { Id = 7, PhoneNumber = "09121230002" });
        _otpRepository.Setup(r => r.TryMarkAsUsedAsync(7)).ReturnsAsync(true);
        Assert.True(await _sut.VerifyOtpAsync("09121230002", "123456"));

        // شمارنده پاک شده ⇒ پنج تلاش ناموفق دیگر هم به بلوک نمی‌رسد
        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);
        for (var i = 0; i < 5; i++)
            Assert.False(await _sut.VerifyOtpAsync("09121230002", "000000"));
    }

    /// <summary>ساعت قابل‌کنترل برای تست انقضای پنجرهٔ قفل</summary>
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    [Fact]
    public async Task LockedPhone_UnblocksAfterTheWindowExpires()
    {
        var clock = new TestClock();
        var sut = new OtpService(_otpRepository.Object, _smsSender.Object, _unitOfWork.Object,
            Mock.Of<ILogger<OtpService>>(), new MemoryCache(new MemoryCacheOptions()), clock);

        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);
        for (var i = 0; i < 5; i++)
            Assert.False(await sut.VerifyOtpAsync("09121231000", "000000"));

        // داخل پنجره: هنوز قفل است
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.GenerateAndSendOtpAsync("09121231000"));

        // با گذشتِ زمانِ پنجره، قفل باز می‌شود (پنجره لغزان نبود ⇒ گذشتِ کم کافی نیست)
        clock.Advance(TimeSpan.FromMinutes(11));

        _otpRepository.Setup(r => r.AddAsync(It.IsAny<OtpCode>())).Returns(Task.CompletedTask);
        _smsSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        await sut.GenerateAndSendOtpAsync("09121231000");

        _otpRepository.Verify(r => r.AddAsync(It.IsAny<OtpCode>()), Times.Once);
    }

    [Fact]
    public async Task SlidingWindowIsNotExtendedByNewAttempts()
    {
        var clock = new TestClock();
        var sut = new OtpService(_otpRepository.Object, _smsSender.Object, _unitOfWork.Object,
            Mock.Of<ILogger<OtpService>>(), new MemoryCache(new MemoryCacheOptions()), clock);

        _otpRepository.Setup(r => r.GetLatestValidAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((OtpCode?)null);

        // ۵ تلاش، هر ۲ دقیقه یکی ⇒ همه در یک پنجرهٔ ۱۰ دقیقه‌ای می‌افتند.
        // اگر پنجره «لغزان» بود (با هر تلاش تمدید)، هرگز به سقف نمی‌رسیدیم.
        for (var i = 0; i < 4; i++)
        {
            await sut.VerifyOtpAsync("09121232000", "000000");
            clock.Advance(TimeSpan.FromMinutes(2)); // جمعاً ۸ دقیقه — هنوز داخل پنجره
        }
        await sut.VerifyOtpAsync("09121232000", "000000"); // تلاش پنجم در دقیقهٔ ۸

        // چون پنجره ثابت است (با هر تلاش تمدید نمی‌شود)، پنجمین تلاش قفل فعال کرده است
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => sut.GenerateAndSendOtpAsync("09121232000"));

        // اما با گذشتِ پنجره باز می‌شود
        clock.Advance(TimeSpan.FromMinutes(11));
        _otpRepository.Setup(r => r.AddAsync(It.IsAny<OtpCode>())).Returns(Task.CompletedTask);
        _smsSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        await sut.GenerateAndSendOtpAsync("09121232000");
        _otpRepository.Verify(r => r.AddAsync(It.IsAny<OtpCode>()), Times.Once);
    }
}
