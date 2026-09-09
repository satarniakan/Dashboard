using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
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

    public async Task AddAsync(SupplierPayment payment) =>
        await _context.SupplierPayments.AddAsync(payment);
}