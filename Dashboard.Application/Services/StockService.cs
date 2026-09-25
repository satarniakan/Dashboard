using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;

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
    Task<StockCountDto> OpenStockCountAsync(int warehouseId, string? userId);
    Task<StockCountDto?> GetStockCountAsync(int id);
    Task<IEnumerable<StockCountSummaryDto>> GetStockCountsAsync();
    Task CloseStockCountAsync(int stockCountId, Dictionary<int, decimal> countedQuantities, string? userId);
}

public class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StockService> _logger;
    private readonly IJournalService _journalService;
    private readonly IStockValidator _stockValidator;

    public StockService(IUnitOfWork unitOfWork, ILogger<StockService> logger, IJournalService journalService, IStockValidator stockValidator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _journalService = journalService;
        _stockValidator = stockValidator;
    }

    // ---------------- انبارها / تأمین‌کنندگان ----------------

    /// <summary>
    /// ایجاد انبار جدید در سیستم
    /// </summary>
    /// <param name="dto">اطلاعات انبار شامل نام، کد، آدرس</param>
    /// <returns>اطلاعات انبار ایجاد شده</returns>
    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseDto dto)
    {
        var warehouse = new Warehouse(dto.Name, dto.Code, dto.Address);
        await _unitOfWork.Warehouses.AddAsync(warehouse);
        await _unitOfWork.CompleteAsync();
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.IsActive);
    }

    /// <summary>
    /// دریافت لیست تمام انبارها
    /// </summary>
    /// <returns>لیست انبارها</returns>
    public async Task<IEnumerable<WarehouseDto>> GetWarehousesAsync()
    {
        var warehouses = await _unitOfWork.Warehouses.GetAllAsync();
        return warehouses.Select(w => new WarehouseDto(w.Id, w.Name, w.Code, w.Address, w.IsActive));
    }

    /// <summary>
    /// ایجاد تأمین‌کننده جدید در سیستم
    /// </summary>
    /// <param name="dto">اطلاعات تأمین‌کننده شامل نام، شخص تماس، تلفن و آدرس</param>
    /// <returns>اطلاعات تأمین‌کننده ایجاد شده</returns>
    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto)
    {
        var supplier = new Supplier(dto.Name, dto.ContactPerson, dto.Phone, dto.Address);
        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.CompleteAsync();
        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactPerson, supplier.Phone, supplier.Address);
    }

    /// <summary>
    /// دریافت لیست تمام تأمین‌کنندگان
    /// </summary>
    /// <returns>لیست تأمین‌کنندگان</returns>
    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        return suppliers.Select(s => new SupplierDto(s.Id, s.Name, s.ContactPerson, s.Phone, s.Address));
    }

    // ---------------- موجودی ----------------

    /// <summary>
    /// دریافت سطح موجودی کالاها در انبارها
    /// </summary>
    /// <param name="warehouseId">شناسه انبار (اختیاری - اگر مشخص نشود همه انبارها را برمی‌گرداند)</param>
    /// <returns>لیست موجودی کالاها</returns>
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

    /// <summary>
    /// دریافت لیست کالاهایی که موجودی آنها زیر حد سفارش مجدد است
    /// </summary>
    /// <returns>لیست کالاهای کم‌موجود</returns>
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

    /// <summary>
    /// دریافت تاریخچه تراکنش‌های موجودی با قابلیت فیلتر بر اساس کالا و انبار
    /// </summary>
    /// <param name="productId">شناسه کالا (اختیاری)</param>
    /// <param name="warehouseId">شناسه انبار (اختیاری)</param>
    /// <returns>لیست تراکنش‌های موجودی</returns>
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

    /// <summary>
    /// ثبت رسید خرید و افزایش موجودی انبار
    /// این متد تراکنش‌های موجودی را ثبت و سند حسابداری (بدهی به تأمین‌کننده) را ایجاد می‌کند
    /// </summary>
    /// <param name="dto">اطلاعات رسید خرید شامل تأمین‌کننده، انبار و اقلام</param>
    /// <param name="userId">شناسه کاربر ثبت‌کننده</param>
    /// <returns>شناسه رسید خرید ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام یا مقادیر نامعتبر</exception>
    public async Task<int> RegisterPurchaseReceiptAsync(CreatePurchaseReceiptDto dto, string? userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            CommonValidations.ValidateItemsNotEmpty(dto.Items);

        var receipt = new PurchaseReceipt
        {
            SupplierId = dto.SupplierId,
            WarehouseId = dto.WarehouseId,
            ReceiptNumber = $"TEMP-{Guid.NewGuid():N}",
            ReceiptDate = dto.ReceiptDate,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        foreach (var item in dto.Items)
        {
            CommonValidations.ValidateQuantityPositive(item.Quantity);

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
        await _unitOfWork.CompleteAsync(); // اینجا receipt.Id واقعی ساخته می‌شود

        receipt.ReceiptNumber = DocumentNumberGenerator.Generate(receipt.ReceiptDate, receipt.SupplierId, receipt.Id);
        await _unitOfWork.PurchaseReceipts.UpdateAsync(receipt);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("PurchaseReceiptRegistered", userId, $"رسید خرید {receipt.ReceiptNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        var totalAmount = receipt.Items.Sum(i => i.Quantity * i.UnitCost);

        if (totalAmount > 0)
        {
            await _journalService.PostEntryAsync(
                description: $"خرید طبق رسید {receipt.ReceiptNumber}",
                lines: new List<JournalLineInput>
                {
            new("1300", totalAmount, 0, "افزایش موجودی کالا"),
            new("2100", 0, totalAmount, "بدهی به تأمین‌کننده", "Supplier", dto.SupplierId)
                },
                referenceType: nameof(PurchaseReceipt),
                referenceId: receipt.Id,
                userId: userId);
        }
        _logger.LogInformation("Purchase receipt {ReceiptNumber} registered by {UserId}", receipt.ReceiptNumber, userId);
            
            await _unitOfWork.CommitTransactionAsync();
            return receipt.Id;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// دریافت لیست خلاصه رسیدهای خرید
    /// </summary>
    /// <returns>لیست رسیدهای خرید</returns>
    public async Task<IEnumerable<PurchaseReceiptSummaryDto>> GetPurchaseReceiptsAsync()
    {
        var receipts = await _unitOfWork.PurchaseReceipts.GetAllAsync();
        return receipts.Select(r => new PurchaseReceiptSummaryDto(
            r.Id, r.ReceiptNumber, r.ReceiptDate,
            r.Supplier?.Name ?? "-", r.Warehouse?.Name ?? "-", r.Items.Count));
    }

    // ---------------- حواله مصرف داخلی ----------------

    /// <summary>
    /// ثبت حواله مصرف داخلی و کاهش موجودی
    /// این متد ابتدا موجودی را بررسی و سپس تراکنش‌های کسر موجودی را ثبت می‌کند
    /// </summary>
    /// <param name="dto">اطلاعات حواله مصرف شامل انبار، هدف مصرف و اقلام</param>
    /// <param name="userId">شناسه کاربر ثبت‌کننده</param>
    /// <returns>شناسه حواله مصرف ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام، مقادیر نامعتبر یا عدم کفایت موجودی</exception>
    public async Task<int> RegisterInternalIssueAsync(CreateInternalIssueDto dto, string? userId)
    {
        CommonValidations.ValidateItemsNotEmpty(dto.Items);

        await _stockValidator.ValidateSufficientStockAsync(dto.WarehouseId, dto.Items);

        var issue = new InternalIssue
        {
            WarehouseId = dto.WarehouseId,
            IssueNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
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
        await _unitOfWork.CompleteAsync(); // اینجا Id واقعی ساخته می‌شود

        issue.IssueNumber = DocumentNumberGenerator.Generate(issue.IssueDate, issue.WarehouseId, issue.Id);
        await _unitOfWork.InternalIssues.UpdateAsync(issue);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("InternalIssueRegistered", userId, $"حواله مصرف داخلی {issue.IssueNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return issue.Id;
    }

    /// <summary>
    /// دریافت لیست خلاصه حواله‌های مصرف داخلی
    /// </summary>
    /// <returns>لیست حواله‌های مصرف</returns>
    public async Task<IEnumerable<InternalIssueSummaryDto>> GetInternalIssuesAsync()
    {
        var issues = await _unitOfWork.InternalIssues.GetAllAsync();
        return issues.Select(i => new InternalIssueSummaryDto(
            i.Id, i.IssueNumber, i.IssueDate, i.Warehouse?.Name ?? "-", i.Purpose, i.Items.Count));
    }

    // ---------------- برگشت از فروش ----------------

    /// <summary>
    /// ثبت برگشت از فروش و افزایش موجودی انبار
    /// </summary>
    /// <param name="dto">اطلاعات برگشت شامل انبار، مرجع مشتری و اقلام</param>
    /// <param name="userId">شناسه کاربر ثبت‌کننده</param>
    /// <returns>شناسه برگشت از فروش ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام</exception>
    public async Task<int> RegisterSalesReturnAsync(CreateSalesReturnDto dto, string? userId)
    {
        CommonValidations.ValidateItemsNotEmpty(dto.Items);

        var salesReturn = new SalesReturn
        {
            WarehouseId = dto.WarehouseId,
            ReturnNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
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
        await _unitOfWork.CompleteAsync(); // اینجا Id واقعی ساخته می‌شود

        salesReturn.ReturnNumber = DocumentNumberGenerator.Generate(salesReturn.ReturnDate, salesReturn.WarehouseId, salesReturn.Id);
        await _unitOfWork.SalesReturns.UpdateAsync(salesReturn);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesReturnRegistered", userId, $"برگشت از فروش {salesReturn.ReturnNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return salesReturn.Id;
    }

    /// <summary>
    /// دریافت لیست خلاصه برگشت‌های از فروش
    /// </summary>
    /// <returns>لیست برگشت‌ها</returns>
    public async Task<IEnumerable<SalesReturnSummaryDto>> GetSalesReturnsAsync()
    {
        var returns = await _unitOfWork.SalesReturns.GetAllAsync();
        return returns.Select(r => new SalesReturnSummaryDto(
            r.Id, r.ReturnNumber, r.ReturnDate, r.Warehouse?.Name ?? "-", r.CustomerReference, r.Items.Count));
    }

    // ---------------- ضایعات ----------------

    /// <summary>
    /// ثبت ضایعات و کاهش موجودی انبار
    /// این متد ابتدا موجودی را بررسی و سپس تراکنش‌های کسر موجودی را به دلیل ضایعات ثبت می‌کند
    /// </summary>
    /// <param name="dto">اطلاعات ضایعات شامل انبار، دلیل و اقلام</param>
    /// <param name="userId">شناسه کاربر ثبت‌کننده</param>
    /// <returns>شناسه سند ضایعات ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام، مقادیر نامعتبر یا عدم کفایت موجودی</exception>
    public async Task<int> RegisterScrapAsync(CreateScrapRecordDto dto, string? userId)
    {
        CommonValidations.ValidateItemsNotEmpty(dto.Items);

        await _stockValidator.ValidateSufficientStockAsync(dto.WarehouseId, dto.Items);

        var scrap = new ScrapRecord
        {
            WarehouseId = dto.WarehouseId,
            RecordNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
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
        await _unitOfWork.CompleteAsync();

        scrap.RecordNumber = DocumentNumberGenerator.Generate(scrap.RecordDate, scrap.WarehouseId, scrap.Id);
        await _unitOfWork.ScrapRecords.UpdateAsync(scrap);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ScrapRegistered", userId, $"ضایعات {scrap.RecordNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return scrap.Id;
    }

    /// <summary>
    /// دریافت لیست خلاصه سوابق ضایعات
    /// </summary>
    /// <returns>لیست ضایعات</returns>
    public async Task<IEnumerable<ScrapRecordSummaryDto>> GetScrapRecordsAsync()
    {
        var records = await _unitOfWork.ScrapRecords.GetAllAsync();
        return records.Select(r => new ScrapRecordSummaryDto(
            r.Id, r.RecordNumber, r.RecordDate, r.Warehouse?.Name ?? "-", r.Reason, r.Items.Count));
    }

    // ---------------- انتقال بین انبار ----------------

    /// <summary>
    /// ثبت انتقال کالا بین دو انبار
    /// این متد ابتدا موجودی انبار مبدا را بررسی، سپس موجودی را از مبدا کسر و به مقصد اضافه می‌کند
    /// </summary>
    /// <param name="dto">اطلاعات انتقال شامل انبار مبدا، مقصد و اقلام</param>
    /// <param name="userId">شناسه کاربر ثبت‌کننده</param>
    /// <returns>شناسه سند انتقال ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام، یکسان بودن انبار مبدا و مقصد یا عدم کفایت موجودی</exception>
    public async Task<int> RegisterStockTransferAsync(CreateStockTransferDto dto, string? userId)
    {
        CommonValidations.ValidateItemsNotEmpty(dto.Items);
        if (dto.SourceWarehouseId == dto.DestinationWarehouseId)
            throw new BusinessRuleException("انبار مبدا و مقصد نمی‌توانند یکسان باشند.");

        await _stockValidator.ValidateSufficientStockAsync(dto.SourceWarehouseId, dto.Items);

        var transfer = new StockTransfer
        {
            SourceWarehouseId = dto.SourceWarehouseId,
            DestinationWarehouseId = dto.DestinationWarehouseId,
            TransferNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
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
        await _unitOfWork.CompleteAsync();

        transfer.TransferNumber = DocumentNumberGenerator.Generate(transfer.TransferDate, transfer.SourceWarehouseId, transfer.Id);
        await _unitOfWork.StockTransfers.UpdateAsync(transfer);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("StockTransferRegistered", userId, $"انتقال {transfer.TransferNumber} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return transfer.Id;
    }

    /// <summary>
    /// دریافت لیست خلاصه انتقالات بین انبار
    /// </summary>
    /// <returns>لیست انتقالات</returns>
    public async Task<IEnumerable<StockTransferSummaryDto>> GetStockTransfersAsync()
    {
        var transfers = await _unitOfWork.StockTransfers.GetAllAsync();
        return transfers.Select(t => new StockTransferSummaryDto(
            t.Id, t.TransferNumber, t.TransferDate,
            t.SourceWarehouse?.Name ?? "-", t.DestinationWarehouse?.Name ?? "-",
            t.Status.ToString(), t.Items.Count));
    }

    // ---------------- انبارگردانی ----------------

    /// <summary>
    /// شروع فرآیند انبارگردانی برای یک انبار
    /// این متد یک سند انبارگردانی باز ایجاد می‌کند که شامل موجودی فعلی تمام کالاهای انبار است
    /// </summary>
    /// <param name="warehouseId">شناسه انبار</param>
    /// <param name="userId">شناسه کاربر ایجادکننده</param>
    /// <returns>اطلاعات سند انبارگردانی ایجاد شده</returns>
    /// <exception cref="NotFoundException">در صورت نبود انبار</exception>
    public async Task<StockCountDto> OpenStockCountAsync(int warehouseId, string? userId)
    {
        var levels = await _unitOfWork.StockLevels.GetByWarehouseAsync(warehouseId);
        var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId)
            ?? throw new NotFoundException("انبار", warehouseId);

        var stockCount = new StockCount
        {
            WarehouseId = warehouseId,
            CountNumber = $"TEMP-{Guid.NewGuid():N}",
            Status = StockCountStatus.Open,
            CreatedByUserId = userId
        };

        foreach (var level in levels)
        {
            stockCount.Items.Add(new StockCountItem
            {
                ProductId = level.ProductId,
                SystemQuantity = level.QuantityOnHand,
                CountedQuantity = level.QuantityOnHand
            });
        }

        await _unitOfWork.StockCounts.AddAsync(stockCount);
        await _unitOfWork.CompleteAsync();

        stockCount.CountNumber = DocumentNumberGenerator.Generate(stockCount.CountDate, stockCount.WarehouseId, stockCount.Id);
        await _unitOfWork.StockCounts.UpdateAsync(stockCount);
        await _unitOfWork.CompleteAsync();

        return new StockCountDto(
         stockCount.Id, stockCount.CountNumber, warehouse.Name, stockCount.Status.ToString(), stockCount.CountDate,
         stockCount.Items.Select(i => new StockCountItemDto(i.ProductId, i.Product?.Name ?? "-", i.SystemQuantity, i.CountedQuantity)).ToList());
    }

    /// <summary>
    /// دریافت جزئیات یک سند انبارگردانی
    /// </summary>
    /// <param name="id">شناسه سند انبارگردانی</param>
    /// <returns>اطلاعات کامل سند انبارگردانی یا null در صورت عدم وجود</returns>
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

    /// <summary>
    /// دریافت لیست خلاصه سوابق انبارگردانی
    /// </summary>
    /// <returns>لیست انبارگردانی‌ها</returns>
    public async Task<IEnumerable<StockCountSummaryDto>> GetStockCountsAsync()
    {
        var counts = await _unitOfWork.StockCounts.GetAllAsync();
        return counts.Select(c => new StockCountSummaryDto(
            c.Id, c.CountNumber, c.CountDate, c.Warehouse?.Name ?? "-", c.Status.ToString()));
    }

    /// <summary>
    /// بستن سند انبارگردانی و اصلاح موجودی بر اساس مقادیر شمارش‌شده
    /// این متد اختلافات موجودی را محاسبه و تراکنش‌های اصلاحی را ثبت می‌کند
    /// </summary>
    /// <param name="stockCountId">شناسه سند انبارگردانی</param>
    /// <param name="countedQuantities">دیکشنری شامل شناسه کالا و مقدار شمارش‌شده</param>
    /// <param name="userId">شناسه کاربر بستن‌کننده</param>
    /// <exception cref="NotFoundException">در صورت نبود سند انبارگردانی</exception>
    /// <exception cref="BusinessRuleException">در صورت بسته شدن قبلی سند</exception>
    public async Task CloseStockCountAsync(int stockCountId, Dictionary<int, decimal> countedQuantities, string? userId)
    {
        var stockCount = await _unitOfWork.StockCounts.GetByIdAsync(stockCountId)
            ?? throw new NotFoundException("سند انبارگردانی", stockCountId);

        if (stockCount.Status == StockCountStatus.Closed)
            throw new BusinessRuleException("این انبارگردانی قبلاً بسته شده است.");

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

    /// <summary>
    /// بررسی کفایت موجودی برای اقلام درخواستی در یک انبار
    /// </summary>
    /// <param name="warehouseId">شناسه انبار</param>
    /// <param name="items">لیست اقلام درخواستی</param>
    /// <exception cref="BusinessRuleException">در صورت مقادیر نامعتبر یا عدم کفایت موجودی</exception>
    private async Task EnsureSufficientStockAsync(int warehouseId, List<StockItemInput> items)
    {
        foreach (var item in items)
        {
            CommonValidations.ValidateQuantityPositive(item.Quantity);

            var level = await _unitOfWork.StockLevels.GetAsync(item.ProductId, warehouseId);
            var available = level?.QuantityOnHand ?? 0;

            if (available < item.Quantity)
                throw new BusinessRuleException(
                    $"موجودی کافی نیست (کالای شماره {item.ProductId}: موجود {available}, درخواستی {item.Quantity}).");
        }
    }
}




