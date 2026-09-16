using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Infrastructure.Repositories;

public class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly AppDbContext _context;
    public SalesInvoiceRepository(AppDbContext context) => _context = context;

    public async Task<SalesInvoice?> GetByIdAsync(int id) =>
        await _context.SalesInvoices
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.Items).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<SalesInvoice>> GetAllAsync() =>
        await _context.SalesInvoices
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.Items)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

    public async Task AddAsync(SalesInvoice invoice) =>
        await _context.SalesInvoices.AddAsync(invoice);

    public Task UpdateAsync(SalesInvoice invoice)
    {
        _context.SalesInvoices.Update(invoice);
        return Task.CompletedTask;
    }

    public async Task<SalesInvoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _context.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x =>
                x.InvoiceNumber == invoiceNumber &&
                x.Status == SalesInvoiceStatus.Confirmed);
    }
}