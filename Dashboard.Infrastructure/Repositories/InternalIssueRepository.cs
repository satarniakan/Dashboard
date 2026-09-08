using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class InternalIssueRepository : IInternalIssueRepository
{
    private readonly AppDbContext _context;
    public InternalIssueRepository(AppDbContext context) => _context = context;

    public async Task<InternalIssue?> GetByIdAsync(int id) =>
        await _context.InternalIssues
            .Include(i => i.Items).ThenInclude(x => x.Product)
            .Include(i => i.Warehouse)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<InternalIssue>> GetAllAsync() =>
        await _context.InternalIssues
            .Include(i => i.Warehouse)
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync();

    public async Task AddAsync(InternalIssue issue) =>
        await _context.InternalIssues.AddAsync(issue);
}
