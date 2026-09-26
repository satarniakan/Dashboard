// Dashboard.Application/Services/DiscountCodeService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface IDiscountCodeService
{
    Task<IEnumerable<DiscountCodeDto>> GetAllAsync();

    /// <summary>ایجاد کد جدید — یکتایی کد و سازگاری مقدار با نوع تخفیف را اعتبارسنجی می‌کند</summary>
    Task<DiscountCodeDto> CreateAsync(CreateDiscountCodeDto dto);

    Task ToggleActiveAsync(int id);

    Task DeleteAsync(int id);
}

public class DiscountCodeService : IDiscountCodeService
{
    private readonly IUnitOfWork _unitOfWork;

    public DiscountCodeService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<IEnumerable<DiscountCodeDto>> GetAllAsync() =>
        (await _unitOfWork.DiscountCodes.GetAllAsync()).Select(ToDto);

    public async Task<DiscountCodeDto> CreateAsync(CreateDiscountCodeDto dto)
    {
        var normalized = dto.Code.Trim().ToUpperInvariant();

        if (await _unitOfWork.DiscountCodes.GetByCodeAsync(normalized) is not null)
            throw new BusinessRuleException("این کد تخفیف قبلاً ثبت شده است.");

        if (dto.Type == DiscountType.Percentage && dto.Value > 100)
            throw new BusinessRuleException("درصد تخفیف نمی‌تواند بیشتر از ۱۰۰ باشد.");

        // مقایسه بعد از نرمال‌سازی پایان‌روز: ورود «شروع ۰۸:۰۰ / انقضا همان روز بدون ساعت» معتبر است
        var startsAt = ToUtc(dto.StartsAt);
        var expiresAt = ToUtc(EndOfDayIfMidnight(dto.ExpiresAt));

        if (startsAt is not null && expiresAt is not null && startsAt > expiresAt)
            throw new BusinessRuleException("تاریخ شروع نمی‌تواند بعد از تاریخ انقضا باشد.");

        var code = new DiscountCode
        {
            Code = normalized,
            Type = dto.Type,
            Value = dto.Value,
            MaxDiscountAmount = dto.MaxDiscountAmount,
            MinCartAmount = dto.MinCartAmount,
            MaxUsageCount = dto.MaxUsageCount,
            // تاریخ‌های واردشده در پنل مدیریت Kind ندارند و به وقت تهران تعبیر می‌شوند؛
            // اعتبارسنجی IsValidNow با UtcNow مقایسه می‌کند، پس این‌جا به UTC نرمال می‌شوند.
            StartsAt = startsAt,
            ExpiresAt = expiresAt
        };

        await _unitOfWork.DiscountCodes.AddAsync(code);
        await _unitOfWork.CompleteAsync();

        return ToDto(code);
    }

    public async Task ToggleActiveAsync(int id)
    {
        var code = await _unitOfWork.DiscountCodes.GetByIdAsync(id)
                   ?? throw new NotFoundException("کد تخفیف", id);

        code.IsActive = !code.IsActive;
        await _unitOfWork.DiscountCodes.UpdateAsync(code);
        await _unitOfWork.CompleteAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _unitOfWork.DiscountCodes.DeleteAsync(id);
        await _unitOfWork.CompleteAsync();
    }

    private static DiscountCodeDto ToDto(DiscountCode d) => new(
        d.Id, d.Code, d.Type, d.Value, d.MaxDiscountAmount, d.MinCartAmount,
        d.MaxUsageCount, d.UsageCount, d.StartsAt, d.ExpiresAt, d.IsActive);

    private static readonly TimeSpan TehranOffset = new(3, 30, 0);

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        // تاریخ بدون Kind (ورودی فرم) به وقت تهران تعبیر می‌شود
        { } unspecified => DateTime.SpecifyKind(unspecified - TehranOffset, DateTimeKind.Utc)
    };

    // ورودی فقط-تاریخ در پنل مدیریت ساعت ۰۰:۰۰ دارد؛ قصد ادمین «تا پایان همان روز» است،
    // وگرنه کد دقیقاً در لحظه‌ی شروع روز انتخابی منقضی می‌شود.
    private static DateTime? EndOfDayIfMidnight(DateTime? value) =>
        value is { Kind: DateTimeKind.Unspecified } v && v.TimeOfDay == TimeSpan.Zero
            ? v.Date.Add(new TimeSpan(23, 59, 59))
            : value;
}
