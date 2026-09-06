using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class OtpCodeRepository : IOtpRepository
{
    private readonly AppDbContext _context;

    public OtpCodeRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(OtpCode otp)
    {
        await _context.OtpCodes.AddAsync(otp);
        //await _context.SaveChangesAsync();
    }

    public async Task<OtpCode?> GetLatestValidAsync(string phoneNumber, string code)
    {
        return await _context.OtpCodes
            .Where(o => o.PhoneNumber == phoneNumber
                        && o.Code == code
                        && !o.IsUsed
                        && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task MarkAsUsedAsync(int id)
    {
        var otp = await _context.OtpCodes.FindAsync(id);
        if (otp is not null)
        {
            otp.IsUsed = true;
            //await _context.SaveChangesAsync();
        }
    }
}