using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly AppDbContext _context;
    public SupplierPaymentRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<SupplierPayment>> GetAllAsync() =>
        await _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.FinancialAccount)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

    public async Task<SupplierPayment?> GetByIdAsync(int id) =>
        await _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.FinancialAccount)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<SupplierPayment>> GetPendingChequesAsync() =>
        await _context.SupplierPayments
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Method == PaymentMethod.Cheque
                     && (p.ChequeStatus == ChequeStatus.Pending || p.ChequeStatus == null))
            .OrderBy(p => p.ChequeDueDate)
            .ToListAsync();

    public async Task AddAsync(SupplierPayment payment) =>
        await _context.SupplierPayments.AddAsync(payment);

    public Task UpdateAsync(SupplierPayment payment)
    {
        _context.SupplierPayments.Update(payment);
        return Task.CompletedTask;
    }

    public async Task<bool> TryClaimChequeStatusAsync(int paymentId, ChequeStatus status)
    {
        var rows = await _context.SupplierPayments
            .Where(p => p.Id == paymentId
                     && p.Method == PaymentMethod.Cheque
                     && (p.ChequeStatus == ChequeStatus.Pending || p.ChequeStatus == null))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ChequeStatus, status));
        return rows > 0;
    }
}