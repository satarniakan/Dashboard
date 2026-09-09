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

    // این همان متد جادویی است که در پایان، همه تغییرات را یک‌باره ذخیره می‌کند
    Task<int> CompleteAsync();
}
