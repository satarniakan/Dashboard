// Dashboard.Infrastructure/Repositories/OutboxRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context) => _context = context;

    // AsNoTracking: نوشتنِ وضعیت با ExecuteUpdateهای شرطی پایین انجام می‌شود؛
    // track‌کردن این رکوردها باعث می‌شد SaveChanges بعدی، مقدارِ کهنهٔ وضعیت را بازنویسی کند.
    public async Task<List<OutboxMessage>> GetPendingAsync(OutboxChannel channel, int maxAttempts, int take) =>
        await _context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Channel == channel && m.Status == OutboxStatus.Pending && m.Attempts < maxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task AddAsync(OutboxMessage message) =>
        await _context.OutboxMessages.AddAsync(message);

    public Task UpdateAsync(OutboxMessage message)
    {
        _context.OutboxMessages.Update(message);
        return Task.CompletedTask;
    }

    public async Task<bool> TryClaimForSendingAsync(int messageId, int maxAttempts)
    {
        var rows = await _context.OutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxStatus.Pending && m.Attempts < maxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Processing)
                .SetProperty(m => m.Attempts, m => m.Attempts + 1));
        return rows > 0;
    }

    public async Task FinishSendingAsync(int messageId, bool success, int attempts, int maxAttempts,
        string? error, DateTime now)
    {
        var target = success
            ? OutboxStatus.Sent
            : attempts >= maxAttempts ? OutboxStatus.Failed : OutboxStatus.Pending;
        var sentAt = success ? (DateTime?)now : null;

        await _context.OutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxStatus.Processing)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, target)
                .SetProperty(m => m.SentAt, sentAt)
                .SetProperty(m => m.LastError, success ? null : error));
    }

    public async Task<int> ReclaimAbandonedAsync() =>
        await _context.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Processing)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Pending));
}
