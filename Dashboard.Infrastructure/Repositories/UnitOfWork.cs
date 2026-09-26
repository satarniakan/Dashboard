using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private int _transactionDepth;

    // در اینجا، مخازن (Repositoryها) را تعریف می‌کنیم
    public IProductRepository Products { get; private set; }
    public IOtpRepository OtpCodes { get; private set; }
    public IAuditLogRepository AuditLogs { get; private set; }

    // --- ماژول انبارداری (WMS) ---
    public IWarehouseRepository Warehouses { get; private set; }
    public ISupplierRepository Suppliers { get; private set; }
    public IStockLevelRepository StockLevels { get; private set; }
    public IStockTransactionRepository StockTransactions { get; private set; }
    public IPurchaseReceiptRepository PurchaseReceipts { get; private set; }
    public IInternalIssueRepository InternalIssues { get; private set; }
    public ISalesReturnRepository SalesReturns { get; private set; }
    public IScrapRecordRepository ScrapRecords { get; private set; }
    public IStockTransferRepository StockTransfers { get; private set; }
    public IStockCountRepository StockCounts { get; private set; }

    // --- ماژول فروش ---
    public ICustomerRepository Customers { get; private set; }
    public ISalesInvoiceRepository SalesInvoices { get; private set; }

    // --- ماژول حسابداری و خزانه‌داری ---
    public IAccountRepository Accounts { get; private set; }
    public IJournalEntryRepository JournalEntries { get; private set; }
    public IFinancialAccountRepository FinancialAccounts { get; private set; }
    public ICustomerReceiptRepository CustomerReceipts { get; private set; }
    public ISupplierPaymentRepository SupplierPayments { get; private set; }
    public IInstallmentPlanRepository InstallmentPlans { get; private set; }
    public ICatalogRepository Catalog { get; private set; }
    public ILocationRepository Locations { get; private set; }
    public ICartRepository Carts { get; private set; }
    public IDiscountCodeRepository DiscountCodes { get; private set; }
    public IOrderRepository Orders { get; private set; }
    public INotificationRepository Notifications { get; private set; }
    public IOutboxRepository Outbox { get; private set; }


    public UnitOfWork(
        AppDbContext context,
        IProductRepository products,
        IOtpRepository otpCodes,
        IAuditLogRepository auditLogs,
        IWarehouseRepository warehouses,
        ISupplierRepository suppliers,
        IStockLevelRepository stockLevels,
        IStockTransactionRepository stockTransactions,
        IPurchaseReceiptRepository purchaseReceipts,
        IInternalIssueRepository internalIssues,
        ISalesReturnRepository salesReturns,
        IScrapRecordRepository scrapRecords,
        IStockTransferRepository stockTransfers,
        IStockCountRepository stockCounts,
        ICustomerRepository customers,
        ISalesInvoiceRepository salesInvoices,
        IAccountRepository accounts,
        IJournalEntryRepository journalEntries,
        IFinancialAccountRepository financialAccounts,
        ICustomerReceiptRepository customerReceipts,
        ISupplierPaymentRepository supplierPayments,
        IInstallmentPlanRepository installmentPlan,
        ICatalogRepository catalog,
        ILocationRepository location,
        ICartRepository carts,
        IDiscountCodeRepository discountCodes,
        IOrderRepository orders,
        INotificationRepository notifications,
        IOutboxRepository outboxRepository

        )
    {
        _context = context;
        Products = products;
        OtpCodes = otpCodes;
        AuditLogs = auditLogs;

        Warehouses = warehouses;
        Suppliers = suppliers;
        StockLevels = stockLevels;
        StockTransactions = stockTransactions;
        PurchaseReceipts = purchaseReceipts;
        InternalIssues = internalIssues;
        SalesReturns = salesReturns;
        ScrapRecords = scrapRecords;
        StockTransfers = stockTransfers;
        StockCounts = stockCounts;

        Customers = customers;
        SalesInvoices = salesInvoices;

        // نکته: این سه خط قبلاً جا افتاده بودند — پراپرتی‌های بالا تعریف شده بودند ولی
        // اینجا مقداردهی نمی‌شدند، پس همیشه null می‌ماندند و اولین استفاده از
        // TreasuryService با NullReferenceException کرش می‌کرد.
        Accounts = accounts;
        JournalEntries = journalEntries;
        FinancialAccounts = financialAccounts;
        CustomerReceipts = customerReceipts;
        SupplierPayments = supplierPayments;
        InstallmentPlans = installmentPlan;
        Catalog = catalog;
        Locations = location;
        Carts = carts;
        DiscountCodes = discountCodes;
        Orders = orders;
        Notifications = notifications;
        Outbox = outboxRepository;
    }

    // این همان متد جادویی است که همه چیز را یک‌باره ذخیره می‌کند
    public async Task<int> CompleteAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        // استثنای همزمانی (RowVersion) باید عیناً به تماس‌گیرنده برسد تا
        // حلقه‌های retry در سرویس‌ها کار کنند؛ DbUpdateConcurrencyException
        // زیرکلاس DbUpdateException است و اگر اول گرفته نشود، گم می‌شود.
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            throw new Dashboard.Domain.Exceptions.BusinessRuleException(
                "این عملیات با یک محدودیت داده‌ای برخورد کرد (مثلاً رکوردی که به این آیتم وابسته است). لطفاً وابستگی‌ها را بررسی کنید.");
        }
    }

    /// <summary>
    /// اجرای عملیات در یک تراکنش دیتابیس، سازگار با EnableRetryOnFailure:
    /// با استراتژی retry، «همه‌ی» دستورات تراکنش باید داخل ExecutionStrategy اجرا شوند —
    /// فقط BeginTransaction را داخل strategy گذاشتن کافی نیست و EF روی اولین کوئری
    /// InvalidOperationException می‌اندازد. فراخوانی تو‌در‌تو در همان تراکنش بیرونی ادغام می‌شود.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        if (_transactionDepth > 0)
        {
            await action();
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _transactionDepth = 1;
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                _transactionDepth = 0;
            }
        });
    }

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();
}
