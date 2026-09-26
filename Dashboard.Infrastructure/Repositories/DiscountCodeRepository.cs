// Dashboard.Infrastructure/Repositories/DiscountCodeRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class DiscountCodeRepository : IDiscountCodeRepository
{
    private readonly AppDbContext _context;

    public DiscountCodeRepository(AppDbContext context) => _context = context;

    public async Task<DiscountCode?> GetByCodeAsync(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.DiscountCodes.FirstOrDefaultAsync(d => d.Code == normalized);
    }

    public async Task<IEnumerable<DiscountCode>> GetAllAsync() =>
        await _context.DiscountCodes.OrderByDescending(d => d.CreatedAt).ToListAsync();

    public async Task<DiscountCode?> GetByIdAsync(int id) =>
        await _context.DiscountCodes.FindAsync(id);

    public async Task AddAsync(DiscountCode discountCode) =>
        await _context.DiscountCodes.AddAsync(discountCode);

    public Task UpdateAsync(DiscountCode discountCode)
    {
        _context.DiscountCodes.Update(discountCode);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id)
    {
        var code = await _context.DiscountCodes.FindAsync(id);
        if (code is not null)
            _context.DiscountCodes.Remove(code);
    }

    public async Task<bool> TryConsumeUsageAsync(int discountCodeId)
    {
        var rows = await _context.DiscountCodes
            .Where(d => d.Id == discountCodeId
                     && (d.MaxUsageCount == null || d.UsageCount < d.MaxUsageCount))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UsageCount, d => d.UsageCount + 1));
        return rows > 0;
    }
}
