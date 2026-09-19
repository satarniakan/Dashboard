using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ISalesInvoiceRepository
{
    Task<SalesInvoice?> GetByIdAsync(int id);
    Task<IEnumerable<SalesInvoice>> GetAllAsync();

    // page از ۱ شروع می‌شود؛ search اختیاری است (جست‌وجو در شماره فاکتور یا نام مشتری)
    Task<(IEnumerable<SalesInvoice> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);

    Task AddAsync(SalesInvoice invoice);
    Task UpdateAsync(SalesInvoice invoice);
    Task<SalesInvoice?> GetByInvoiceNumberAsync(string invoiceNumber);
}