using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

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
        IStockCountRepository stockCounts)
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
    }

    // این همان متد جادویی است که همه چیز را یک‌باره ذخیره می‌کند
    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
