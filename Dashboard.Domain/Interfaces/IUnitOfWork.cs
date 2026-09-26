namespace Dashboard.Domain.Interfaces;

public interface IUnitOfWork
{
    // این‌ها همان انباردارهایی هستند که سرکارگر باید به آن‌ها دسترسی داشته باشد
    IProductRepository Products { get; }
    IOtpRepository OtpCodes { get; }
    IAuditLogRepository AuditLogs { get; }

    // --- ماژول انبارداری (WMS) ---
    IWarehouseRepository Warehouses { get; }
    ISupplierRepository Suppliers { get; }
    IStockLevelRepository StockLevels { get; }
    IStockTransactionRepository StockTransactions { get; }
    IPurchaseReceiptRepository PurchaseReceipts { get; }
    IInternalIssueRepository InternalIssues { get; }
    ISalesReturnRepository SalesReturns { get; }
    IScrapRecordRepository ScrapRecords { get; }
    IStockTransferRepository StockTransfers { get; }
    IStockCountRepository StockCounts { get; }

    // --- ماژول فروش ---
    ICustomerRepository Customers { get; }
    ISalesInvoiceRepository SalesInvoices { get; }

    // --- ماژول حسابداری و خزانه‌داری ---
    IAccountRepository Accounts { get; }
    IJournalEntryRepository JournalEntries { get; }
    IFinancialAccountRepository FinancialAccounts { get; }
    ICustomerReceiptRepository CustomerReceipts { get; }
    ISupplierPaymentRepository SupplierPayments { get; }
    IInstallmentPlanRepository InstallmentPlans { get; }
    ICatalogRepository Catalog { get; }
    ILocationRepository Locations { get; }

    // --- فروشگاه اینترنتی ---
    ICartRepository Carts { get; }
    IDiscountCodeRepository DiscountCodes { get; }
    IOrderRepository Orders { get; }
    INotificationRepository Notifications { get; }
    IOutboxRepository Outbox { get; }

    // این همان متد جادویی است که در پایان، همه تغییرات را یک‌باره ذخیره می‌کند
    Task<int> CompleteAsync();

    // برای عملیات‌های چندمرحله‌ای (مثل تأیید فاکتور یا تولید دیتای تستی) که یا باید کامل
    // انجام بشن یا اصلاً هیچی ثبت نشه. کل عملیات داخل ExecutionStrategy و یک تراکنش اجرا
    // می‌شود (سازگار با EnableRetryOnFailure)؛ در صورت استثنا تراکنش رول‌بک می‌شود.
    // فراخوانی تو‌در‌تو مجاز است: لایه‌ی داخلی در همان تراکنش بیرونی اجرا می‌شود.
    Task ExecuteInTransactionAsync(Func<Task> action);

    /// <summary>
    /// پاک‌کردن change tracker — بعد از rollback و قبل از تلاش مجدد لازم است تا
    /// انتیتی‌های Added/Modified مانده از attempt قبلی باعث درج تکراری نشوند.
    /// </summary>
    void ClearChangeTracker();
}
