// Dashboard.Domain/Interfaces/IOtpRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(OtpCode otp);
    Task<OtpCode?> GetLatestValidAsync(string phoneNumber, string code);
    Task MarkAsUsedAsync(int id);
}