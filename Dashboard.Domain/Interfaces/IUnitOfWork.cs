namespace Dashboard.Domain.Interfaces;

public interface IUnitOfWork
{
    // این‌ها همان انباردارهایی هستند که سرکارگر باید به آن‌ها دسترسی داشته باشد
    IProductRepository Products { get; }
    IOtpRepository OtpCodes { get; }
    IAuditLogRepository AuditLogs { get; }

    // ماژول انبارداری و فروش (فاز ۱)
    IWarehouseRepository Warehouses { get; }
    IStockRepository Stock { get; }
    ICustomerRepository Customers { get; }
    ISalesInvoiceRepository SalesInvoices { get; }

    // این همان متد جادویی است که در پایان، همه تغییرات را یک‌باره ذخیره می‌کند
    Task<int> CompleteAsync();
}