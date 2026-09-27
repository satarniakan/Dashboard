using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Accounting;
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
    private readonly INotificationService _notifications;

    public StockService(IUnitOfWork unitOfWork, ILogger<StockService> logger, IJournalService journalService, IStockValidator stockValidator, INotificationService notifications)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _journalService = journalService;
        _stockValidator = stockValidator;
        _notifications = notifications;
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
        CommonValidations.ValidateItemsNotEmpty(dto.Items);

        PurchaseReceipt receipt = null!;
        // RunInTransactionAsync (نه ExecuteInTransactionAsync) چون با RowVersion روی محصول،
        // دو رسید خریدِ هم‌زمانِ یک کالا می‌توانند تصادم بگیرند؛ این پوشش، تلاش مجدد می‌کند
        await RunInTransactionAsync(async () =>
        {
            // موجودی هر کالا قبل از این رسید (مبنای میانگین موزون)
            var previousQuantities = new Dictionary<int, decimal>();

            receipt = new PurchaseReceipt
            {
                SupplierId = dto.SupplierId,
                WarehouseId = dto.WarehouseId,
                ReceiptNumber = $"TEMP-{Guid.NewGuid():N}",
                ReceiptDate = dto.ReceiptDate,
                Notes = dto.Notes,
                CreatedByUserId = userId
            };

            // هر کالا فقط یک‌بار پردازش می‌شود: فاکتور ممکن است چند سطر برای یک کالا داشته باشد
            // و بدون جمع‌کردن، «موجودی قبل از رسید» هر سطر روی سطر قبلی بازنویسی می‌شد و
            // میانگین موزون را غلط حساب می‌کرد (مثلاً ۵۵٬۰۰۰ به‌جای ۷۰٬۰۰۰).
            foreach (var group in dto.Items.GroupBy(i => i.ProductId))
            {
                foreach (var item in group)
                    CommonValidations.ValidateQuantityPositive(item.Quantity);

                var incomingQuantity = group.Sum(i => i.Quantity);
                var incomingCost = group.Sum(i => i.Quantity * i.UnitCost);

                receipt.Items.Add(new PurchaseReceiptItem
                {
                    ProductId = group.Key,
                    Quantity = incomingQuantity,
                    UnitCost = incomingQuantity == 0 ? 0 : incomingCost / incomingQuantity
                });

                // موجودی قبل از این رسید (مبنای میانگین موزون) — یک‌بار برای هر کالا
                var levelBefore = await _unitOfWork.StockLevels.GetAsync(group.Key, dto.WarehouseId);
                previousQuantities[group.Key] = levelBefore?.QuantityOnHand ?? 0m;

                await _unitOfWork.StockLevels.IncreaseOrCreateAsync(group.Key, dto.WarehouseId, incomingQuantity);
            }

            // هزینه‌یابی میانگین موزون: بهای تمام‌شدهٔ کالا با هر خرید به‌روز می‌شود تا
            // سود ناخالص فروش‌های بعدی با قیمت واقعیِ خرید محاسبه شود (نه قیمت اولیهٔ محصول)
            // کالاها یک‌جا بارگذاری می‌شوند تا برای هر سطر یک کوئری اضافه نزنیم (N+1)
            var incomingByProduct = receipt.Items.ToDictionary(i => i.ProductId);
            var productIds = incomingByProduct.Keys.ToList();
            var products = (await _unitOfWork.Products.GetByIdsAsync(productIds))
                .ToDictionary(p => p.Id);

            foreach (var productId in productIds)
            {
                if (!products.TryGetValue(productId, out var product))
                    throw new NotFoundException("کالا", productId);

                var previousQuantity = previousQuantities[productId];
                var incomingQuantity = incomingByProduct[productId].Quantity;
                var incomingUnitCost = incomingByProduct[productId].UnitCost;

                product.ApplyWeightedAverageCost(previousQuantity, incomingQuantity, incomingUnitCost);
                await _unitOfWork.Products.UpdateAsync(product);

                _logger.LogInformation(
                    "Weighted average cost for product {ProductId} updated to {Cost} (previous qty {PreviousQty}, incoming {IncomingQty} @ {UnitCost})",
                    productId, product.CostPrice, previousQuantity, incomingQuantity, incomingUnitCost);
            }

            await _unitOfWork.PurchaseReceipts.AddAsync(receipt);
            await _unitOfWork.CompleteAsync(); // اینجا receipt.Id واقعی ساخته می‌شود

            // تراکنش‌های موجودی بعد از ساخت Id ثبت می‌شوند تا ReferenceId به سند مبدا قابل ردیابی باشد.
            // از receipt.Items (تجمیع‌شده) استفاده می‌شود تا تعداد تراکنش‌ها با سطرهای رسید یکی بماند.
            foreach (var item in receipt.Items)
            {
                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.WarehouseId,
                    Type = StockTransactionType.PurchaseReceipt,
                    QuantityChange = item.Quantity,
                    UnitCost = item.UnitCost,
                    ReferenceType = nameof(PurchaseReceipt),
                    ReferenceId = receipt.Id,
                    CreatedByUserId = userId
                });
            }

            receipt.ReceiptNumber = DocumentNumberGenerator.Generate(receipt.ReceiptDate, receipt.SupplierId, receipt.Id);
            await _unitOfWork.PurchaseReceipts.UpdateAsync(receipt);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("PurchaseReceiptRegistered", userId, $"رسید خرید {receipt.ReceiptNumber} ثبت شد."));
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "رسید خرید ثبت شد", $"رسید {receipt.ReceiptNumber} با {receipt.Items.Count} قلم کالا",
                NotificationType.System, "/warehouse/purchase-receipts");
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
        });

        return receipt.Id;
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

        var issueId = 0;
        await RunInTransactionAsync(async () =>
        {
            // چک اولیه (پیام خطای تجمیعی) — محافظ نهایی، کسر اتمیک DecreaseWithCheckAsync است
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
                issue.Items.Add(new InternalIssueItem { ProductId = item.ProductId, Quantity = item.Quantity });

            // ابتدا سند ثبت و Id واقعی ساخته می‌شود تا تراکنش‌های موجودی بتوانند ReferenceId داشته باشند
            await _unitOfWork.InternalIssues.AddAsync(issue);
            await _unitOfWork.CompleteAsync(); // اینجا Id واقعی ساخته می‌شود
            issueId = issue.Id;

            foreach (var item in dto.Items)
            {
                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.WarehouseId,
                    Type = StockTransactionType.InternalIssue,
                    QuantityChange = -item.Quantity,
                    ReferenceType = nameof(InternalIssue),
                    ReferenceId = issue.Id,
                    CreatedByUserId = userId
                });

                // کسر اتمیک با بررسی کفایت + RowVersion — جایگزین check-then-decrement غیراتمی
                await _unitOfWork.StockLevels.DecreaseWithCheckAsync(item.ProductId, dto.WarehouseId, item.Quantity);
            }

            issue.IssueNumber = DocumentNumberGenerator.Generate(issue.IssueDate, issue.WarehouseId, issue.Id);
            await _unitOfWork.InternalIssues.UpdateAsync(issue);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("InternalIssueRegistered", userId, $"حواله مصرف داخلی {issue.IssueNumber} ثبت شد."));
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "حواله مصرف داخلی ثبت شد", $"حواله {issue.IssueNumber} با {dto.Items.Count} قلم کالا",
                NotificationType.System, "/warehouse/internal-issues");
            await _unitOfWork.CompleteAsync();

            // خروج کالا از انبار باید در دفتر کل هم بنشیند، وگرنه ماندهٔ ۱۳۰۰ (موجودی کالا)
            // از جمع تراکنش‌های انبار جدا می‌افتد و تراز آزمایشی دیگر با انبار نمی‌خواند.
            var issuedCost = await SumCostByQuantityAsync(
                dto.Items.Select(i => (i.ProductId, i.Quantity)).ToList());
            if (issuedCost > 0)
            {
                await _journalService.PostEntryAsync(
                    description: $"حواله مصرف داخلی {issue.IssueNumber}",
                    lines: new List<JournalLineInput>
                    {
                        new(SystemAccountCodes.InternalIssueExpense, issuedCost, 0, "مصرف داخلی کالا"),
                        new(SystemAccountCodes.Inventory, 0, issuedCost, "خروج کالا از انبار")
                    },
                    referenceType: nameof(InternalIssue),
                    referenceId: issue.Id,
                    userId: userId);
            }
        });

        return issueId;
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

        var salesReturnId = 0;
        await RunInTransactionAsync(async () =>
        {
            // ⚠️ اعتبارسنجی مقدار باید بیرون از شاخهٔ «فاکتور مرجع» هم اجرا شود.
            // پیش‌تر این بررسی فقط داخل `if (SalesInvoiceId is int)` بود و مرجوعیِ بدون
            // فاکتور هیچ کنترلی نداشت؛ مقدار منفی یک تراکنشِ منفی (کسرِ موجودی به‌جای
            // برگشت) می‌ساخت. دیتابیس فقط با CK_StockLevels_NonNegative جلوی فاجعه را می‌گرفت.
            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                    throw new BusinessRuleException("مقدار برگشتی باید بزرگ‌تر از صفر باشد.");
            }

            // فاکتور مرجع اختیاری است، ولی برای برگشتِ حسابداری لازم است: بدون آن
            // نمی‌دانیم چه مبلغی و با چه بهای تمام‌شده‌ای باید برگردد
            SalesInvoice? sourceInvoice = null;
            if (dto.SalesInvoiceId is int invoiceId)
            {
                sourceInvoice = await _unitOfWork.SalesInvoices.GetByIdAsync(invoiceId)
                    ?? throw new NotFoundException("فاکتور", invoiceId);

                if (sourceInvoice.WarehouseId != dto.WarehouseId)
                    throw new BusinessRuleException("انبار فاکتور با انبار برگشتی یکسان نیست.");

                // برگشت روی فاکتور پیش‌نویس یعنی سندِ برگشتِ درآمد بدون سندِ فروش
                if (sourceInvoice.Status != SalesInvoiceStatus.Confirmed)
                    throw new BusinessRuleException(
                        $"برگشت فقط روی فاکتور تأییدشده ممکن است (وضعیت فعلی: {sourceInvoice.Status}).");

                // مقدار برگشتی مثبت بودنش در ابتدای تراکنش (بیرون از این شاخه) بررسی شد

                // GroupBy به‌جای ToDictionary: فاکتور ممکن است چند سطر برای یک کالا داشته باشد
                var soldByProduct = sourceInvoice.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                // مبنای مجاز، «فروش‌رفته منهای برگشت‌های قبلی» است؛ وگرنه یک فاکتور ۱۰تایی را
                // می‌شد ۱۰ بار، هر بار یک عدد، برگرداند و موجودی/درآمد چندبار کم شود
                var alreadyReturnedByProduct = (await _unitOfWork.SalesReturns.GetBySalesInvoiceIdAsync(sourceInvoice.Id))
                    .SelectMany(r => r.Items)
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                foreach (var item in dto.Items.GroupBy(i => i.ProductId))
                {
                    var requested = item.Sum(x => x.Quantity);

                    if (!soldByProduct.TryGetValue(item.Key, out var sold))
                        throw new BusinessRuleException(
                            $"کالای شماره {item.Key} در فاکتور مرجع نبوده است.");

                    var alreadyReturned = alreadyReturnedByProduct.GetValueOrDefault(item.Key);
                    var remaining = sold - alreadyReturned;

                    if (requested > remaining)
                        throw new BusinessRuleException(
                            $"مقدار برگشتی ({requested:0.##}) بیشتر از مقدار قابل‌برگشت ({remaining:0.##}) است. " +
                            $"در فاکتور {sold:0.##} فروخته و {alreadyReturned:0.##} قبلاً برگشته شده است.");
                }
            }

            var salesReturn = new SalesReturn
            {
                WarehouseId = dto.WarehouseId,
                ReturnNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
                ReturnDate = dto.ReturnDate,
                CustomerReference = dto.CustomerReference,
                Notes = dto.Notes,
                SalesInvoiceId = sourceInvoice?.Id,
                CreatedByUserId = userId
            };

            foreach (var item in dto.Items)
                salesReturn.Items.Add(new SalesReturnItem { ProductId = item.ProductId, Quantity = item.Quantity });

            // ابتدا سند ثبت و Id واقعی ساخته می‌شود تا تراکنش‌های موجودی بتوانند ReferenceId داشته باشند
            await _unitOfWork.SalesReturns.AddAsync(salesReturn);
            await _unitOfWork.CompleteAsync(); // اینجا Id واقعی ساخته می‌شود
            salesReturnId = salesReturn.Id;

            foreach (var item in dto.Items)
            {
                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.WarehouseId,
                    Type = StockTransactionType.SalesReturn,
                    QuantityChange = item.Quantity,
                    ReferenceType = nameof(SalesReturn),
                    ReferenceId = salesReturn.Id,
                    CreatedByUserId = userId
                });

                await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.WarehouseId, item.Quantity);
            }

            salesReturn.ReturnNumber = DocumentNumberGenerator.Generate(salesReturn.ReturnDate, salesReturn.WarehouseId, salesReturn.Id);
            await _unitOfWork.SalesReturns.UpdateAsync(salesReturn);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesReturnRegistered", userId, $"برگشت از فروش {salesReturn.ReturnNumber} ثبت شد."));

            // سند حسابداری برگشت — فقط وقتی فاکتور مرجع دارد (وگرنه مبلغِ برگشتی مبهم است).
            // معکوس سند فروش به‌ازای همان اقلام: کاهش درآمد و طلب مشتری، برگشت موجودی و COGS
            if (sourceInvoice is not null)
            {
                // فاکتور مرجع می‌تواند برای یک کالا چند سطر داشته باشد؛ GroupBy لازم است
                // چون ToDictionary با کلید تکراری خطا می‌داد و برگشت را کاملاً متوقف می‌کرد.
                // میانگین «وزنی» گرفته می‌شود تا درآمد برگشتی با فروش اصلی هم‌خوان بماند.
                var invoiceItems = sourceInvoice.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var qty = g.Sum(x => x.Quantity);
                        return (
                            UnitPrice: qty == 0 ? 0 : g.Sum(x => x.Quantity * x.UnitPrice) / qty,
                            CostPrice: g.Sum(x => x.CostPrice ?? 0)
                        );
                    });

                var returnLines = new List<DTOs.JournalLineInput>();
                var revenueTotal = 0m;
                var grossReturned = 0m;

                foreach (var group in dto.Items.GroupBy(i => i.ProductId))
                {
                    var returnedQty = group.Sum(x => x.Quantity);
                    if (!invoiceItems.TryGetValue(group.Key, out var invoiceItem))
                        throw new BusinessRuleException($"کالای شماره {group.Key} در فاکتور مرجع نبوده است.");

                    var lineRevenue = returnedQty * invoiceItem.UnitPrice;
                    var lineCost = returnedQty * invoiceItem.CostPrice;
                    revenueTotal += lineRevenue;
                    grossReturned += lineRevenue;
                }

                // ⚠️ تخفیفِ فاکتور مرجع هم باید کسر شود، وگرنه برگشتِ جزئی از فاکتورِ تخفیف‌دار
                // بیش از مبلغ واقعیِ پرداخت‌شده طلب مشتری را کم می‌کرد (و درآمد جعلی می‌ساخت).
                // سهم تخفیف به نسبتِ ارزشِ خامِ برگشتی به کل ارزشِ خامِ فاکتور محاسبه می‌شود.
                var invoiceGross = sourceInvoice.TotalAmount - sourceInvoice.ShippingAmount
                                   + sourceInvoice.DiscountAmount;
                var discountShare = invoiceGross > 0
                    ? sourceInvoice.DiscountAmount * (grossReturned / invoiceGross)
                    : 0m;
                var netRevenueTotal = Math.Max(revenueTotal - discountShare, 0m);

                foreach (var group in dto.Items.GroupBy(i => i.ProductId))
                {
                    var returnedQty = group.Sum(x => x.Quantity);
                    var invoiceItem = invoiceItems[group.Key];
                    var lineRevenue = returnedQty * invoiceItem.UnitPrice;
                    var lineCost = returnedQty * invoiceItem.CostPrice;

                    // سهم تخفیف همین قلم (نسبتِ این قلم از کل برگشتی)
                    var lineDiscount = revenueTotal > 0 ? discountShare * (lineRevenue / revenueTotal) : 0m;
                    var netLineRevenue = Math.Max(lineRevenue - lineDiscount, 0m);

                    returnLines.Add(new DTOs.JournalLineInput(
                        SystemAccountCodes.SalesRevenue, netLineRevenue, 0, "برگشت درآمد فروش"));
                    returnLines.Add(new DTOs.JournalLineInput(
                        SystemAccountCodes.Inventory, lineCost, 0, "برگشت موجودی کالا"));
                    returnLines.Add(new DTOs.JournalLineInput(
                        SystemAccountCodes.CostOfGoodsSold, 0, lineCost, "برگشت بهای تمام‌شده"));
                }

                // طلب مشتری به اندازهٔ مبلغ فروشِ برگشتی (بعد از تخفیف) کم می‌شود
                returnLines.Add(new DTOs.JournalLineInput(
                    SystemAccountCodes.AccountsReceivable, 0, netRevenueTotal,
                    "کاهش طلب مشتری بابت برگشت از فروش", "Customer", sourceInvoice.CustomerId));

                await _journalService.PostEntryAsync(
                    description: $"برگشت از فروش طبق سند {salesReturn.ReturnNumber}",
                    lines: returnLines,
                    referenceType: nameof(SalesReturn),
                    referenceId: salesReturn.Id,
                    userId: userId);
            }
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "برگشت از فروش ثبت شد", $"برگشت {salesReturn.ReturnNumber} با {dto.Items.Count} قلم کالا",
                NotificationType.System, "/warehouse/sales-returns");
            await _unitOfWork.CompleteAsync();
        });

        return salesReturnId;
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

        var scrapId = 0;
        await RunInTransactionAsync(async () =>
        {
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
                scrap.Items.Add(new ScrapRecordItem { ProductId = item.ProductId, Quantity = item.Quantity });

            // ابتدا سند ثبت و Id واقعی ساخته می‌شود تا تراکنش‌های موجودی بتوانند ReferenceId داشته باشند
            await _unitOfWork.ScrapRecords.AddAsync(scrap);
            await _unitOfWork.CompleteAsync();
            scrapId = scrap.Id;

            foreach (var item in dto.Items)
            {
                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.WarehouseId,
                    Type = StockTransactionType.Scrap,
                    QuantityChange = -item.Quantity,
                    ReferenceType = nameof(ScrapRecord),
                    ReferenceId = scrap.Id,
                    CreatedByUserId = userId
                });

                // کسر اتمیک با بررسی کفایت + RowVersion
                await _unitOfWork.StockLevels.DecreaseWithCheckAsync(item.ProductId, dto.WarehouseId, item.Quantity);
            }

            scrap.RecordNumber = DocumentNumberGenerator.Generate(scrap.RecordDate, scrap.WarehouseId, scrap.Id);
            await _unitOfWork.ScrapRecords.UpdateAsync(scrap);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("ScrapRegistered", userId, $"ضایعات {scrap.RecordNumber} ثبت شد."));
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "ضایعات ثبت شد", $"سند ضایعات {scrap.RecordNumber} با {dto.Items.Count} قلم کالا",
                NotificationType.System, "/warehouse/scrap");
            await _unitOfWork.CompleteAsync();

            // ضایعات هم مثل حواله باید سند بخورد (بدهکار هزینه ضایعات / بستانکار موجودی کالا)
            var scrapCost = await SumCostByQuantityAsync(
                dto.Items.Select(i => (i.ProductId, i.Quantity)).ToList());
            if (scrapCost > 0)
            {
                await _journalService.PostEntryAsync(
                    description: $"ضایعات {scrap.RecordNumber}",
                    lines: new List<JournalLineInput>
                    {
                        new(SystemAccountCodes.ScrapExpense, scrapCost, 0, "هزینه ضایعات انبار"),
                        new(SystemAccountCodes.Inventory, 0, scrapCost, "خروج کالا از انبار بابت ضایعات")
                    },
                    referenceType: nameof(ScrapRecord),
                    referenceId: scrap.Id,
                    userId: userId);
            }
        });

        return scrapId;
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

        var transferId = 0;
        await RunInTransactionAsync(async () =>
        {
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
                transfer.Items.Add(new StockTransferItem { ProductId = item.ProductId, Quantity = item.Quantity });

            // ابتدا سند ثبت و Id واقعی ساخته می‌شود تا تراکنش‌های موجودی بتوانند ReferenceId داشته باشند
            await _unitOfWork.StockTransfers.AddAsync(transfer);
            await _unitOfWork.CompleteAsync();
            transferId = transfer.Id;

            foreach (var item in dto.Items)
            {
                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.SourceWarehouseId,
                    Type = StockTransactionType.TransferOut,
                    QuantityChange = -item.Quantity,
                    ReferenceType = nameof(StockTransfer),
                    ReferenceId = transfer.Id,
                    CreatedByUserId = userId
                });
                // کسر اتمیک از مبدا با بررسی کفایت + RowVersion
                await _unitOfWork.StockLevels.DecreaseWithCheckAsync(item.ProductId, dto.SourceWarehouseId, item.Quantity);

                await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                {
                    ProductId = item.ProductId,
                    WarehouseId = dto.DestinationWarehouseId,
                    Type = StockTransactionType.TransferIn,
                    QuantityChange = item.Quantity,
                    ReferenceType = nameof(StockTransfer),
                    ReferenceId = transfer.Id,
                    CreatedByUserId = userId
                });
                await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, dto.DestinationWarehouseId, item.Quantity);
            }

            transfer.TransferNumber = DocumentNumberGenerator.Generate(transfer.TransferDate, transfer.SourceWarehouseId, transfer.Id);
            await _unitOfWork.StockTransfers.UpdateAsync(transfer);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("StockTransferRegistered", userId, $"انتقال {transfer.TransferNumber} ثبت شد."));
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "انتقال بین انبار ثبت شد", $"سند {transfer.TransferNumber} با {dto.Items.Count} قلم کالا",
                NotificationType.System, "/warehouse/transfers");
            await _unitOfWork.CompleteAsync();
        });

        return transferId;
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

        StockCount? stockCount = null;
        await RunInTransactionAsync(async () =>
        {
            stockCount = new StockCount
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
        });

        return new StockCountDto(
         stockCount!.Id, stockCount.CountNumber, warehouse.Name, stockCount.Status.ToString(), stockCount.CountDate,
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
        await RunInTransactionAsync(async () =>
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

                // اصلاح کاهشی با کف‌سنجی اتمیک (موجودی منفی نشود)؛ اصلاح افزاینه بدون ریسک است
                if (discrepancy < 0)
                    await _unitOfWork.StockLevels.DecreaseWithCheckAsync(item.ProductId, stockCount.WarehouseId, -discrepancy);
                else
                    await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, stockCount.WarehouseId, discrepancy);
            }

            stockCount.Status = StockCountStatus.Closed;
            stockCount.ClosedAt = DateTime.UtcNow;

            await _unitOfWork.StockCounts.UpdateAsync(stockCount);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("StockCountClosed", userId, $"انبارگردانی {stockCount.CountNumber} بسته شد."));
            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.WarehouseUser,
                "انبارگردانی بسته شد", $"انبارگردانی {stockCount.CountNumber} بسته و موجودی‌ها تصحیح شد",
                NotificationType.System, "/warehouse/stock-counts");
            await _unitOfWork.CompleteAsync();

            // اختلاف شمارش باید به حسابِ خودِ اختلاف برود تا ۱۳۰۰ با انبار هم‌خوان بماند:
            // کسری → بدهکار «کسری انبار»، اضافه → بستانکار «اضافات کشف‌شده».
            var discrepancies = stockCount.Items
                .Select(i => (i.ProductId, i.Discrepancy))
                .Where(i => i.Discrepancy != 0)
                .ToList();
            var shortageCost = await SumCostByQuantityAsync(
                discrepancies.Where(i => i.Discrepancy < 0).Select(i => (i.ProductId, -i.Discrepancy)).ToList());
            var surplusCost = await SumCostByQuantityAsync(
                discrepancies.Where(i => i.Discrepancy > 0).Select(i => (i.ProductId, i.Discrepancy)).ToList());

            var countLines = new List<JournalLineInput>();
            if (shortageCost > 0)
            {
                countLines.Add(new JournalLineInput(SystemAccountCodes.InventoryShortage, shortageCost, 0, "کسری شمارش انبارگردانی"));
                countLines.Add(new JournalLineInput(SystemAccountCodes.Inventory, 0, shortageCost, "اصلاح موجودی بابت کسری"));
            }
            if (surplusCost > 0)
            {
                countLines.Add(new JournalLineInput(SystemAccountCodes.Inventory, surplusCost, 0, "اصلاح موجودی بابت اضافات"));
                countLines.Add(new JournalLineInput(SystemAccountCodes.InventorySurplus, 0, surplusCost, "اضافات کشف‌شده در انبارگردانی"));
            }
            if (countLines.Count > 0)
            {
                await _journalService.PostEntryAsync(
                    description: $"اختلاف انبارگردانی {stockCount.CountNumber}",
                    lines: countLines,
                    referenceType: nameof(StockCount),
                    referenceId: stockCount.Id,
                    userId: userId);
            }
        });
    }

    // ---------------- کمکی ----------------

    /// <summary>
    /// جمع بهای تمام‌شدهٔ اقلام (مقدار × CostPrice جاری = میانگین موزون) برای سند انبار.
    /// یک کوئری تجمیعی برای همهٔ کالاها — نه به‌ازای هر سطر (N+1).
    /// </summary>
    private async Task<decimal> SumCostByQuantityAsync(IReadOnlyCollection<(int ProductId, decimal Quantity)> items)
    {
        if (items.Count == 0) return 0m;

        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.GetByIdsAsync(ids);
        var costs = products.ToDictionary(p => p.Id, p => p.CostPrice);

        return Math.Round(items.Sum(i => i.Quantity * (costs.TryGetValue(i.ProductId, out var c) ? c : 0m)), 2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// اجرای یک عملیات چندمرحله‌ای در تراکنش دیتابیس با retry روی تصادم همزمانی (RowVersion).
    /// قبل از هر تلاش دوباره، change tracker پاک می‌شود تا انتیتی‌های Attempt قبلی
    /// (Added/Modified مانده پس از rollback) باعث درج تکراری یا خطای همزمانی مجدد نشوند.
    /// </summary>
    private async Task RunInTransactionAsync(Func<Task> action)
    {
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(action);
                return;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _unitOfWork.ClearChangeTracker();
                _logger.LogWarning("Concurrency conflict in stock operation, retrying (attempt {Attempt})", attempt);
            }
        }
    }
}




