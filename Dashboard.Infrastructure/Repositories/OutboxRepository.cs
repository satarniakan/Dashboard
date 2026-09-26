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

    public async Task<List<OutboxMessage>> GetPendingAsync(OutboxChannel channel, int maxAttempts, int take) =>
        await _context.OutboxMessages
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
}
