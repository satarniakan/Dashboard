using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class InstallmentPlanRepository : IInstallmentPlanRepository
{
    private readonly AppDbContext _context;
    public InstallmentPlanRepository(AppDbContext context) => _context = context;

    public async Task<InstallmentPlan?> GetBySalesInvoiceIdAsync(int salesInvoiceId) =>
        await _context.InstallmentPlans
            .Include(p => p.Installments)
            .Include(p => p.SalesInvoice).ThenInclude(i => i!.Customer)
            .FirstOrDefaultAsync(p => p.SalesInvoiceId == salesInvoiceId);

    public async Task<Installment?> GetInstallmentByIdAsync(int installmentId) =>
        await _context.Installments
            .Include(i => i.InstallmentPlan).ThenInclude(p => p!.SalesInvoice)
            .FirstOrDefaultAsync(i => i.Id == installmentId);

    public async Task AddAsync(InstallmentPlan plan) =>
        await _context.InstallmentPlans.AddAsync(plan);

    public async Task<IEnumerable<Installment>> GetOverdueInstallmentsAsync() =>
        await _context.Installments
            .Include(i => i.InstallmentPlan).ThenInclude(p => p!.SalesInvoice).ThenInclude(inv => inv!.Customer)
            .Where(i => i.DueDate.Date < DateTime.UtcNow.Date && i.PaidAmount < i.Amount)
            .OrderBy(i => i.DueDate)
            .ToListAsync();

    public async Task<IEnumerable<Installment>> GetAllInstallmentsAsync() =>
        await _context.Installments
            .Include(i => i.InstallmentPlan).ThenInclude(p => p!.SalesInvoice).ThenInclude(inv => inv!.Customer)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
}