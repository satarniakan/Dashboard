using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class CustomerReceiptRepository : ICustomerReceiptRepository
{
    private readonly AppDbContext _context;
    public CustomerReceiptRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<CustomerReceipt>> GetAllAsync() =>
        await _context.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.FinancialAccount)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();

    public async Task<CustomerReceipt?> GetByIdAsync(int id) =>
        await _context.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.FinancialAccount)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<CustomerReceipt>> GetPendingChequesAsync() =>
        await _context.CustomerReceipts
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.Method == PaymentMethod.Cheque
                     && (r.ChequeStatus == ChequeStatus.Pending || r.ChequeStatus == null))
            .OrderBy(r => r.ChequeDueDate)
            .ToListAsync();

    public async Task AddAsync(CustomerReceipt receipt) =>
        await _context.CustomerReceipts.AddAsync(receipt);

    public Task UpdateAsync(CustomerReceipt receipt)
    {
        _context.CustomerReceipts.Update(receipt);
        return Task.CompletedTask;
    }

    public async Task<bool> TryClaimChequeStatusAsync(int receiptId, ChequeStatus status)
    {
        var rows = await _context.CustomerReceipts
            .Where(r => r.Id == receiptId
                     && r.Method == PaymentMethod.Cheque
                     && (r.ChequeStatus == ChequeStatus.Pending || r.ChequeStatus == null))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ChequeStatus, status));
        return rows > 0;
    }
}