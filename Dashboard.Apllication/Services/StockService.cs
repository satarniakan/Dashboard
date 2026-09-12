using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IStockService
{
    // انبارها و تأمین‌کنندگان
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseDto dto);
    Task<IEnumerable<WarehouseDto>> GetWarehousesAsync();
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto);
    Task<IEnumerable<SupplierDto>> GetSuppliersAsync();

    // موجودی
    Task<IEnumerable<StockLevelDto>> GetStockLevelsAsync(int? warehouseId = null);
    Task<IEnumerable<StockLevelDto>> GetLowStockAsync();
    Task<IEnumerable<StockTransactionDto>> GetStockHistoryAsync(int? productId = null, int? warehouseId = null);

    // رویدادهای انبار (۲-۳ در سند: رسید خرید / حواله مصرف / برگشت از فروش / ضایعات)
    Task<int> RegisterPurchaseReceiptAsync(CreatePurchaseReceiptDto dto, string? userId);
    Task<int> RegisterInternalIssueAsync(CreateInternalIssueDto dto, string? userId);
    Task<int> RegisterSalesReturnAsync(CreateSalesReturnDto dto, string? userId);
    Task<int> RegisterScrapAsync(CreateScrapRecordDto dto, string? userId);

    Task<IEnumerable<PurchaseReceiptSummaryDto>> GetPurchaseReceiptsAsync();
    Task<IEnumerable<InternalIssueSummaryDto>> GetInternalIssuesAsync();
    Task<IEnumerable<SalesReturnSummaryDto>> GetSalesReturnsAsync();
    Task<IEnumerable<ScrapRecordSummaryDto>> GetScrapRecordsAsync();

    // انتقال بین انبار (۲-۲)
    Task<int> RegisterStockTransferAsync(CreateStockTransferDto dto, string? userId);
    Task<IEnumerable<StockTransferSummaryDto>> GetStockTransfersAsync();

    // انبارگردانی (۲-۵)
    Task<StockCountDto> OpenStockCountAsync(int warehouseId, string countNumber, string? userId);
    Task<StockCountDto?> GetStockCountAsync(int id);
    Task<IEnumerable<StockCountSummaryDto>> GetStockCountsAsync();
    Task CloseStockCountAsync(int stockCountId, Dictionary<int, decimal> countedQuantities, string? userId);
}

public class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StockService> _logger;
    private readonly IJournalService _journalService;
    // ... تو Constructor:

    public StockService(IUnitOfWork unitOfWork, ILogger<StockService> logger, IJournalService journalService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _journalService = journalService;
    }

    // ---------------- انبارها / تأمین‌کنندگان ----------------

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseDto dto)
    {
        var warehouse = new Warehouse(dto.Name, dto.Code, dto.Address);
        await _unitOfWork.Warehouses.AddAsync(warehouse);
        await _unitOfWork.CompleteAsync();
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.IsActive);
    }

    public async Task<IEnumerable<WarehouseDto>> GetWarehousesAsync()
    {
        var warehouses = await _unitOfWork.Warehouses.GetAllAsync();
        return warehouses.Select(w => new WarehouseDto(w.Id, w.Name, w.Code, w.Address, w.IsActive));
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto)
    {
        var supplier = new Supplier(dto.Name, dto.ContactPerson, dto.Phone, dto.Address);
        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.CompleteAsync();
        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactPerson, supplier.Phone, supplier.Address);
    }

    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        return suppliers.Select(s => new SupplierDto(s.Id, s.Name, s.ContactPerson, s.Phone, s.Address));
    }

    // ---------------- موجودی ----------------

    public async Task<IEnumerable<StockLevelDto>> GetStockLevelsAsync(int? warehouseId = null)
    {
        var levels = warehouseId.HasValue
            ? await _unitOfWork.StockLevels.GetByWarehouseAsync(warehouseId.Value)
            : await _unitOfWork.StockLevels.GetAllAsync();

        return levels
            .Where(l => l.Product is not null && l.Warehouse is not null)
            .Select(l => new StockLevelDto(
                l.ProductId, l.Product!.Name, l.Product.Sku,
                l.WarehouseId, l.Warehouse!.Name,
                l.QuantityOnHand, l.Product.ReorderPoint));
    }

    public async Task<IEnumerable<StockLevelDto>> GetLowStockAsync()
    {
        var levels = await _unitOfWork.StockLevels.GetBelowReorderPointAsync();
        return levels
            .Where(l => l.Product is not null && l.Warehouse is not null)
            .Select(l => new StockLevelDto(
                l.ProductId, l.Product!.Name, l.Product.Sku,
                l.WarehouseId, l.Warehouse!.Name,
                l.QuantityOnHand, l.Product.ReorderPoint));
    }

    public async Task<IEnumerable<StockTransactionDto>> GetStockHistoryAsync(int? productId = null, int? warehouseId = null)
    {
        var history = await _unitOfWork.StockTransactions.GetHistoryAsync(productId, warehouseId);
        return history.Select(t => new StockTransactionDto(
            t.Id,
            t.Product?.Name ?? "-",
            t.Warehouse?.Name ?? "-",
            t.Type.ToString(),
            t.QuantityChange,
            t.UnitCost,
            t.Notes,
            t.CreatedByUserId,
            t.OccurredAt));
    }

    // ---------------- رسید خرید ----------------

    public async Task<int> RegisterPurchaseReceiptAsync(CreatePurchaseReceiptDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");

        var receipt = new PurchaseReceipt
        {
            SupplierId = dto.SupplierId,
            WarehouseId = dto.WarehouseId,
            ReceiptNumber = dto.ReceiptNumber,
            ReceiptDate = dto.ReceiptDate,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("مقدار باید بزرگتر از صفر باشد.");

            receipt.Items.Add(new PurchaseReceiptItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost
            });

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.WarehouseId,
                Type = StockTransactionType.PurchaseReceipt,
                QuantityChange = item.Quantity,
                UnitCost = item.UnitCost,
                ReferenceType = nameof(PurchaseReceipt),
                CreatedByUserId = userId
            });

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.WarehouseId, item.Quantity);
        }

        await _unitOfWork.PurchaseReceipts.AddAsync(receipt);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("PurchaseReceiptRegistered", userId, $"رسید خرید {dto.ReceiptNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();
        var totalAmount = receipt.Items.Sum(i => i.Quantity * i.UnitCost);

        if (totalAmount > 0)
        {
            await _journalService.PostEntryAsync(
                description: $"خرید طبق رسید {dto.ReceiptNumber}",
                lines: new List<JournalLineInput>
                {
            new("1300", totalAmount, 0, "افزایش موجودی کالا"),
            new("2100", 0, totalAmount, "بدهی به تأمین‌کننده", "Supplier", dto.SupplierId)
                },
                referenceType: nameof(PurchaseReceipt),
                referenceId: receipt.Id,
                userId: userId);
        }
        _logger.LogInformation("Purchase receipt {ReceiptNumber} registered by {UserId}", dto.ReceiptNumber, userId);
        return receipt.Id;
    }

    public async Task<IEnumerable<PurchaseReceiptSummaryDto>> GetPurchaseReceiptsAsync()
    {
        var receipts = await _unitOfWork.PurchaseReceipts.GetAllAsync();
        return receipts.Select(r => new PurchaseReceiptSummaryDto(
            r.Id, r.ReceiptNumber, r.ReceiptDate,
            r.Supplier?.Name ?? "-", r.Warehouse?.Name ?? "-", r.Items.Count));
    }

    // ---------------- حواله مصرف داخلی ----------------

    public async Task<int> RegisterInternalIssueAsync(CreateInternalIssueDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");

        await EnsureSufficientStockAsync(dto.WarehouseId, dto.Items);

        var issue = new InternalIssue
        {
            WarehouseId = dto.WarehouseId,
            IssueNumber = dto.IssueNumber,
            IssueDate = dto.IssueDate,
            Purpose = dto.Purpose,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            issue.Items.Add(new InternalIssueItem { ProductId = item.ProductId, Quantity = item.Quantity });

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.WarehouseId,
                Type = StockTransactionType.InternalIssue,
                QuantityChange = -item.Quantity,
                ReferenceType = nameof(InternalIssue),
                CreatedByUserId = userId
            });

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.WarehouseId, -item.Quantity);
        }

        await _unitOfWork.InternalIssues.AddAsync(issue);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("InternalIssueRegistered", userId, $"حواله مصرف داخلی {dto.IssueNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return issue.Id;
    }

    public async Task<IEnumerable<InternalIssueSummaryDto>> GetInternalIssuesAsync()
    {
        var issues = await _unitOfWork.InternalIssues.GetAllAsync();
        return issues.Select(i => new InternalIssueSummaryDto(
            i.Id, i.IssueNumber, i.IssueDate, i.Warehouse?.Name ?? "-", i.Purpose, i.Items.Count));
    }

    // ---------------- برگشت از فروش ----------------

    public async Task<int> RegisterSalesReturnAsync(CreateSalesReturnDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");

        var salesReturn = new SalesReturn
        {
            WarehouseId = dto.WarehouseId,
            ReturnNumber = dto.ReturnNumber,
            ReturnDate = dto.ReturnDate,
            CustomerReference = dto.CustomerReference,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            salesReturn.Items.Add(new SalesReturnItem { ProductId = item.ProductId, Quantity = item.Quantity });

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.WarehouseId,
                Type = StockTransactionType.SalesReturn,
                QuantityChange = item.Quantity,
                ReferenceType = nameof(SalesReturn),
                CreatedByUserId = userId
            });

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.WarehouseId, item.Quantity);
        }

        await _unitOfWork.SalesReturns.AddAsync(salesReturn);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesReturnRegistered", userId, $"برگشت از فروش {dto.ReturnNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return salesReturn.Id;
    }

    public async Task<IEnumerable<SalesReturnSummaryDto>> GetSalesReturnsAsync()
    {
        var returns = await _unitOfWork.SalesReturns.GetAllAsync();
        return returns.Select(r => new SalesReturnSummaryDto(
            r.Id, r.ReturnNumber, r.ReturnDate, r.Warehouse?.Name ?? "-", r.CustomerReference, r.Items.Count));
    }

    // ---------------- ضایعات ----------------

    public async Task<int> RegisterScrapAsync(CreateScrapRecordDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");

        await EnsureSufficientStockAsync(dto.WarehouseId, dto.Items);

        var scrap = new ScrapRecord
        {
            WarehouseId = dto.WarehouseId,
            RecordNumber = dto.RecordNumber,
            RecordDate = dto.RecordDate,
            Reason = dto.Reason,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            scrap.Items.Add(new ScrapRecordItem { ProductId = item.ProductId, Quantity = item.Quantity });

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.WarehouseId,
                Type = StockTransactionType.Scrap,
                QuantityChange = -item.Quantity,
                ReferenceType = nameof(ScrapRecord),
                CreatedByUserId = userId
            });

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.WarehouseId, -item.Quantity);
        }

        await _unitOfWork.ScrapRecords.AddAsync(scrap);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ScrapRegistered", userId, $"ضایعات {dto.RecordNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return scrap.Id;
    }

    public async Task<IEnumerable<ScrapRecordSummaryDto>> GetScrapRecordsAsync()
    {
        var records = await _unitOfWork.ScrapRecords.GetAllAsync();
        return records.Select(r => new ScrapRecordSummaryDto(
            r.Id, r.RecordNumber, r.RecordDate, r.Warehouse?.Name ?? "-", r.Reason, r.Items.Count));
    }

    // ---------------- انتقال بین انبار ----------------

    public async Task<int> RegisterStockTransferAsync(CreateStockTransferDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");
        if (dto.SourceWarehouseId == dto.DestinationWarehouseId)
            throw new InvalidOperationException("انبار مبدا و مقصد نمی‌توانند یکسان باشند.");

        await EnsureSufficientStockAsync(dto.SourceWarehouseId, dto.Items);

        var transfer = new StockTransfer
        {
            SourceWarehouseId = dto.SourceWarehouseId,
            DestinationWarehouseId = dto.DestinationWarehouseId,
            TransferNumber = dto.TransferNumber,
            TransferDate = dto.TransferDate,
            Notes = dto.Notes,
            Status = StockTransferStatus.Completed,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            transfer.Items.Add(new StockTransferItem { ProductId = item.ProductId, Quantity = item.Quantity });

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.SourceWarehouseId,
                Type = StockTransactionType.TransferOut,
                QuantityChange = -item.Quantity,
                ReferenceType = nameof(StockTransfer),
                CreatedByUserId = userId
            });
            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.SourceWarehouseId, -item.Quantity);

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = dto.DestinationWarehouseId,
                Type = StockTransactionType.TransferIn,
                QuantityChange = item.Quantity,
                ReferenceType = nameof(StockTransfer),
                CreatedByUserId = userId
            });
            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.DestinationWarehouseId, item.Quantity);
        }

        await _unitOfWork.StockTransfers.AddAsync(transfer);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("StockTransferRegistered", userId, $"انتقال {dto.TransferNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return transfer.Id;
    }

    public async Task<IEnumerable<StockTransferSummaryDto>> GetStockTransfersAsync()
    {
        var transfers = await _unitOfWork.StockTransfers.GetAllAsync();
        return transfers.Select(t => new StockTransferSummaryDto(
            t.Id, t.TransferNumber, t.TransferDate,
            t.SourceWarehouse?.Name ?? "-", t.DestinationWarehouse?.Name ?? "-",
            t.Status.ToString(), t.Items.Count));
    }

    // ---------------- انبارگردانی ----------------

    public async Task<StockCountDto> OpenStockCountAsync(int warehouseId, string countNumber, string? userId)
    {
        var levels = await _unitOfWork.StockLevels.GetByWarehouseAsync(warehouseId);
        var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId)
            ?? throw new InvalidOperationException("انبار یافت نشد.");

        var stockCount = new StockCount
        {
            WarehouseId = warehouseId,
            CountNumber = countNumber,
            Status = StockCountStatus.Open,
            CreatedByUserId = userId
        };

        foreach (var level in levels)
        {
            stockCount.Items.Add(new StockCountItem
            {
                ProductId = level.ProductId,
                SystemQuantity = level.QuantityOnHand,
                CountedQuantity = level.QuantityOnHand // پیش‌فرض؛ کاربر در فرم شمارش تغییرش می‌دهد
            });
        }

        await _unitOfWork.StockCounts.AddAsync(stockCount);
        await _unitOfWork.CompleteAsync();

        return new StockCountDto(
            stockCount.Id, stockCount.CountNumber, warehouse.Name, stockCount.Status.ToString(), stockCount.CountDate,
            stockCount.Items.Select(i => new StockCountItemDto(i.ProductId, i.Product?.Name ?? "-", i.SystemQuantity, i.CountedQuantity)).ToList());
    }

    public async Task<StockCountDto?> GetStockCountAsync(int id)
    {
        var stockCount = await _unitOfWork.StockCounts.GetByIdAsync(id);
        if (stockCount is null) return null;

        return new StockCountDto(
            stockCount.Id, stockCount.CountNumber, stockCount.Warehouse?.Name ?? "-",
            stockCount.Status.ToString(), stockCount.CountDate,
            stockCount.Items.Select(i => new StockCountItemDto(
                i.ProductId, i.Product?.Name ?? "-", i.SystemQuantity, i.CountedQuantity)).ToList());
    }

    public async Task<IEnumerable<StockCountSummaryDto>> GetStockCountsAsync()
    {
        var counts = await _unitOfWork.StockCounts.GetAllAsync();
        return counts.Select(c => new StockCountSummaryDto(
            c.Id, c.CountNumber, c.CountDate, c.Warehouse?.Name ?? "-", c.Status.ToString()));
    }

    public async Task CloseStockCountAsync(int stockCountId, Dictionary<int, decimal> countedQuantities, string? userId)
    {
        var stockCount = await _unitOfWork.StockCounts.GetByIdAsync(stockCountId)
            ?? throw new InvalidOperationException("سند انبارگردانی یافت نشد.");

        if (stockCount.Status == StockCountStatus.Closed)
            throw new InvalidOperationException("این انبارگردانی قبلاً بسته شده است.");

        foreach (var item in stockCount.Items)
        {
            if (countedQuantities.TryGetValue(item.ProductId, out var counted))
                item.CountedQuantity = counted;

            var discrepancy = item.Discrepancy;
            if (discrepancy == 0) continue;

            await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = stockCount.WarehouseId,
                Type = discrepancy > 0 ? StockTransactionType.StockCountIncrease : StockTransactionType.StockCountDecrease,
                QuantityChange = discrepancy,
                ReferenceType = nameof(StockCount),
                ReferenceId = stockCount.Id,
                Notes = $"اصلاح انبارگردانی {stockCount.CountNumber}",
                CreatedByUserId = userId
            });

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, stockCount.WarehouseId, discrepancy);
        }

        stockCount.Status = StockCountStatus.Closed;
        stockCount.ClosedAt = DateTime.UtcNow;

        await _unitOfWork.StockCounts.UpdateAsync(stockCount);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("StockCountClosed", userId, $"انبارگردانی {stockCount.CountNumber} بسته شد."));
        await _unitOfWork.CompleteAsync();
    }

    // ---------------- کمکی ----------------

    private async Task EnsureSufficientStockAsync(int warehouseId, List<StockItemInput> items)
    {
        foreach (var item in items)
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("مقدار باید بزرگتر از صفر باشد.");

            var level = await _unitOfWork.StockLevels.GetAsync(item.ProductId, warehouseId);
            var available = level?.QuantityOnHand ?? 0;

            if (available < item.Quantity)
                throw new InvalidOperationException(
                    $"موجودی کافی نیست (کالای شماره {item.ProductId}: موجود {available}, درخواستی {item.Quantity}).");
        }
    }
}
