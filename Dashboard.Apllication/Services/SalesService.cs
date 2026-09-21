using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dashboard.Application.Services;

public interface ISalesService
{
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto);
    Task<IEnumerable<CustomerDto>> GetCustomersAsync();

    Task<int> CreateDraftInvoiceAsync(CreateSalesInvoiceDto dto, string? userId);
    Task<SalesInvoiceDto?> GetInvoiceAsync(int id);
    Task<IEnumerable<SalesInvoiceSummaryDto>> GetInvoicesAsync();
    Task<PagedResult<SalesInvoiceSummaryDto>> GetInvoicesPagedAsync(int page, int pageSize, string? search = null);

    Task ConfirmInvoiceAsync(int invoiceId, string? userId);
    Task CancelInvoiceAsync(int invoiceId, string? userId);
    Task<SalesInvoiceDto?> GetInvoiceByNumberAsync(string invoiceNumber);
}

public class SalesService : ISalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJournalService _journalService;
    private readonly ILogger<SalesService> _logger;
    private readonly IStockValidator _stockValidator;

    public SalesService(IUnitOfWork unitOfWork, IJournalService journalService, ILogger<SalesService> logger, IStockValidator stockValidator)
    {
        _unitOfWork = unitOfWork;
        _journalService = journalService;
        _logger = logger;
        _stockValidator = stockValidator;
    }

    // ---------------- مشتریان ----------------

    /// <summary>
    /// ایجاد مشتری جدید در سیستم
    /// </summary>
    /// <param name="dto">اطلاعات مشتری شامل نام، تلفن و آدرس</param>
    /// <returns>اطلاعات مشتری ایجاد شده با شناسه منحصربه‌فرد</returns>
    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto)
    {
        var customer = new Customer(dto.Name, dto.Phone, dto.Address);
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.CompleteAsync();
        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Address);
    }

    /// <summary>
    /// دریافت لیست تمام مشتریان
    /// </summary>
    /// <returns>لیست اطلاعات مشتریان</returns>
    public async Task<IEnumerable<CustomerDto>> GetCustomersAsync()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        return customers.Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Address));
    }

    // ---------------- ساخت پیش‌نویس فاکتور ----------------

    /// <summary>
    /// ایجاد فاکتور فروش به صورت پیش‌نویس (بدون تأثیر بر موجودی)
    /// </summary>
    /// <param name="dto">اطلاعات فاکتور شامل مشتری، انبار، اقلام و تخفیف</param>
    /// <param name="userId">شناسه کاربر ایجادکننده</param>
    /// <returns>شناسه فاکتور ایجاد شده</returns>
    /// <exception cref="BusinessRuleException">در صورت نبود اقلام یا مقادیر نامعتبر</exception>
    public async Task<int> CreateDraftInvoiceAsync(CreateSalesInvoiceDto dto, string? userId)
    {
        CommonValidations.ValidateItemsNotEmpty(dto.Items);

        var invoice = new SalesInvoice
        {
            CustomerId = dto.CustomerId,
            WarehouseId = dto.WarehouseId,
            InvoiceNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
            InvoiceDate = dto.InvoiceDate,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Status = SalesInvoiceStatus.Draft,
            CreatedByUserId = userId
        };

        decimal total = 0;
        foreach (var item in dto.Items)
        {
            CommonValidations.ValidateQuantityPositive(item.Quantity);
            CommonValidations.ValidatePriceNonNegative(item.UnitPrice);

            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });

            total += item.Quantity * item.UnitPrice;
        }

        invoice.TotalAmount = total - dto.DiscountAmount;

        await _unitOfWork.SalesInvoices.AddAsync(invoice);
        await _unitOfWork.CompleteAsync(); // اینجا Id واقعی ساخته می‌شود

        invoice.InvoiceNumber = DocumentNumberGenerator.Generate(invoice.InvoiceDate, invoice.CustomerId, invoice.Id);
        await _unitOfWork.SalesInvoices.UpdateAsync(invoice);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesInvoiceDrafted", userId, $"فاکتور {invoice.InvoiceNumber} به‌صورت پیش‌نویس ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return invoice.Id;
    }

    // ---------------- تأیید فاکتور (اینجا موجودی واقعاً کسر می‌شود) ----------------

    /// <summary>
    /// تأیید فاکتور پیش‌نویس و کسر موجودی از انبار
    /// این متد ابتدا موجودی تمام اقلام را بررسی و سپس تراکنش‌های موجودی و سند حسابداری را ثبت می‌کند
    /// </summary>
    /// <param name="invoiceId">شناسه فاکتور</param>
    /// <param name="userId">شناسه کاربر تأییدکننده</param>
    /// <exception cref="NotFoundException">در صورت نبود فاکتور</exception>
    /// <exception cref="BusinessRuleException">در صورت عدم کفایت موجودی یا وضعیت نامعتبر فاکتور</exception>
    public async Task ConfirmInvoiceAsync(int invoiceId, string? userId)
    {
        // Race condition روی موجودی: چون بین خواندن و نوشتن موجودی، درخواست‌های همزمان می‌توانند
        // همان رکورد را بخوانند، به‌جای Validate جداگانه از DecreaseWithCheckAsync (اتمیک) استفاده
        // می‌کنیم و با تکیه بر RowVersion (همزمانی خوش‌بینانه) در صورت تصادم، عملیات را از نو تلاش می‌کنیم.
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(invoiceId)
                ?? throw new NotFoundException("فاکتور", invoiceId);

                if (invoice.Status != SalesInvoiceStatus.Draft)
                    throw new BusinessRuleException("فقط فاکتور پیش‌نویس قابل تأیید است.");

                // کسر اتمیک موجودی برای هر قلم؛ اگر کافی نباشد استثنا پرتاب می‌شود
                // و کل تراکنش رول‌بک می‌شود (بنابراین اگر یکی کم بود، هیچ‌کدام کسر نمی‌شود)
                foreach (var item in invoice.Items)
                {
                    await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        WarehouseId = invoice.WarehouseId,
                        Type = StockTransactionType.Sale,
                        QuantityChange = -item.Quantity,
                        ReferenceType = nameof(SalesInvoice),
                        ReferenceId = invoice.Id,
                        CreatedByUserId = userId
                    });

                    await _unitOfWork.StockLevels.DecreaseWithCheckAsync(item.ProductId, invoice.WarehouseId, item.Quantity);
                }

                invoice.Status = SalesInvoiceStatus.Confirmed;
                invoice.ConfirmedAt = DateTime.UtcNow;

                await _unitOfWork.SalesInvoices.UpdateAsync(invoice);
                await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesInvoiceConfirmed", userId, $"فاکتور {invoice.InvoiceNumber} تأیید و موجودی کسر شد."));
                await _unitOfWork.CompleteAsync();

                // بهای تمام‌شده از روی CostPrice لحظه‌ای کالا محاسبه می‌شود (نه میانگین موزون واقعی)؛
                // برای فاز اول کافی است، اما اگر کنترل دقیق‌تر سود ناخالص لازم شد باید این را
                // به یک روش هزینه‌یابی واقعی (FIFO/میانگین موزون) ارتقا داد.
                var totalCost = invoice.Items.Sum(i => i.Quantity * (i.Product?.CostPrice ?? 0));

                // سند فروش: بدهکار مشتری (طلب) و بدهکار COGS، بستانکار درآمد فروش و بستانکار موجودی کالا
                await _journalService.PostEntryAsync(
                    description: $"فروش طبق فاکتور {invoice.InvoiceNumber}",
                    lines: new List<JournalLineInput>
                    {
                        new(SystemAccountCodes.AccountsReceivable, invoice.TotalAmount, 0, "بدهکار شدن حساب مشتری", "Customer", invoice.CustomerId),
                        new(SystemAccountCodes.SalesRevenue, 0, invoice.TotalAmount, "شناسایی درآمد فروش"),
                        new(SystemAccountCodes.CostOfGoodsSold, totalCost, 0, "بهای تمام‌شده کالای فروش‌رفته"),
                        new(SystemAccountCodes.Inventory, 0, totalCost, "کاهش موجودی کالا")
                    },
                    referenceType: nameof(SalesInvoice),
                    referenceId: invoice.Id,
                    userId: userId);

                _logger.LogInformation("Sales invoice {InvoiceNumber} confirmed by {UserId}", invoice.InvoiceNumber, userId);

                await _unitOfWork.CommitTransactionAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                // رکورد موجودی توسط یک درخواست همزمان دیگر تغییر کرده است؛ رول‌بک و تلاش دوباره از ابتدا
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogWarning("Concurrency conflict while confirming invoice {InvoiceId}, retrying (attempt {Attempt})", invoiceId, attempt);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }

    // ---------------- لغو فاکتور (برگشت موجودی) ----------------

    /// <summary>
    /// لغو فاکتور و برگشت موجودی به انبار (در صورت تأیید قبلی)
    /// این متد سند حسابداری معکوس را نیز ثبت می‌کند تا اثر فاکتور خنثی شود
    /// </summary>
    /// <param name="invoiceId">شناسه فاکتور</param>
    /// <param name="userId">شناسه کاربر لغوکننده</param>
    /// <exception cref="NotFoundException">در صورت نبود فاکتور</exception>
    /// <exception cref="BusinessRuleException">در صورت لغو قبلی فاکتور</exception>
    public async Task CancelInvoiceAsync(int invoiceId, string? userId)
    {
        // مانند ConfirmInvoiceAsync، برگشت موجودی هم روی رکورد StockLevel با RowVersion محافظت می‌شود؛
        // در صورت تصادم همزمانی، تراکنش رول‌بک و از نو تلاش می‌شود.
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(invoiceId)
                ?? throw new NotFoundException("فاکتور", invoiceId);

                if (invoice.Status == SalesInvoiceStatus.Canceled)
                    throw new BusinessRuleException("این فاکتور قبلاً لغو شده است.");

                var wasConfirmed = invoice.Status == SalesInvoiceStatus.Confirmed;

                if (wasConfirmed)
                {
                    foreach (var item in invoice.Items)
                    {
                        await _unitOfWork.StockTransactions.AddAsync(new StockTransaction
                        {
                            ProductId = item.ProductId,
                            WarehouseId = invoice.WarehouseId,
                            Type = StockTransactionType.SaleCancellation,
                            QuantityChange = item.Quantity,
                            ReferenceType = nameof(SalesInvoice),
                            ReferenceId = invoice.Id,
                            CreatedByUserId = userId
                        });

                        await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, invoice.WarehouseId, item.Quantity);
                    }
                }

                invoice.Status = SalesInvoiceStatus.Canceled;
                invoice.CanceledAt = DateTime.UtcNow;

                await _unitOfWork.SalesInvoices.UpdateAsync(invoice);
                await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesInvoiceCanceled", userId, $"فاکتور {invoice.InvoiceNumber} لغو شد."));
                await _unitOfWork.CompleteAsync();
                if (wasConfirmed)
                {
                    var totalCost = invoice.Items.Sum(i => i.Quantity * (i.Product?.CostPrice ?? 0));

                    // سند برگشت، دقیقاً برعکس سند فروش اصلی است تا اثر آن به‌طور کامل خنثی شود
                    await _journalService.PostEntryAsync(
                        description: $"برگشت از فروش طبق لغو فاکتور {invoice.InvoiceNumber}",
                        lines: new List<JournalLineInput>
                        {
                            new(SystemAccountCodes.SalesRevenue, invoice.TotalAmount, 0, "برگشت درآمد فروش"),
                            new(SystemAccountCodes.AccountsReceivable, 0, invoice.TotalAmount, "بستانکار شدن حساب مشتری", "Customer", invoice.CustomerId),
                            new(SystemAccountCodes.Inventory, totalCost, 0, "برگشت موجودی کالا"),
                            new(SystemAccountCodes.CostOfGoodsSold, 0, totalCost, "برگشت بهای تمام‌شده")
                        },
                        referenceType: nameof(SalesInvoice),
                        referenceId: invoice.Id,
                        userId: userId);
                }

                await _unitOfWork.CommitTransactionAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogWarning("Concurrency conflict while canceling invoice {InvoiceId}, retrying (attempt {Attempt})", invoiceId, attempt);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }

    // ---------------- خواندن ----------------

    /// <summary>
    /// دریافت جزئیات یک فاکتور بر اساس شناسه
    /// </summary>
    /// <param name="id">شناسه فاکتور</param>
    /// <returns>اطلاعات کامل فاکتور یا null در صورت عدم وجود</returns>
    public async Task<SalesInvoiceDto?> GetInvoiceAsync(int id)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(id);
        if (invoice is null) return null;

        return new SalesInvoiceDto(
            invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
            invoice.Customer?.Name, invoice.Warehouse?.Name ?? "-", invoice.WarehouseId,invoice.CustomerId, invoice.Status.ToString(),
            invoice.DiscountAmount, invoice.TotalAmount, invoice.Notes,
            invoice.Items.Select(i => new SalesInvoiceItemDto(
                i.ProductId, i.Product?.Name ?? "-", i.Quantity, i.UnitPrice, i.LineTotal)).ToList());
    }

    /// <summary>
    /// دریافت لیست خلاصه تمام فاکتورهای فروش
    /// </summary>
    /// <returns>لیست خلاصه فاکتورها</returns>
    public async Task<IEnumerable<SalesInvoiceSummaryDto>> GetInvoicesAsync()
    {
        var invoices = await _unitOfWork.SalesInvoices.GetAllAsync();
        return invoices.Select(i => new SalesInvoiceSummaryDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.Customer?.Name, i.Warehouse?.Name ?? "-",
            i.Status.ToString(), i.TotalAmount));
    }

    /// <summary>
    /// دریافت جزئیات فاکتور بر اساس شماره فاکتور
    /// </summary>
    /// <param name="invoiceNumber">شماره فاکتور</param>
    /// <returns>اطلاعات کامل فاکتور یا null در صورت عدم وجود</returns>
    public async Task<SalesInvoiceDto?> GetInvoiceByNumberAsync(string invoiceNumber)
    {
        var invoice = await _unitOfWork.SalesInvoices
     .GetByInvoiceNumberAsync(invoiceNumber);

        if (invoice is null)
            return null;

        return new SalesInvoiceDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.Customer?.Name,
            invoice.Warehouse?.Name ?? "-",
            invoice.WarehouseId,
            invoice.CustomerId,
            invoice.Status.ToString(),
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.Notes,
            invoice.Items.Select(i => new SalesInvoiceItemDto(
                i.ProductId,
                i.Product?.Name ?? "-",
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            )).ToList()
        );
    }

    /// <summary>
    /// دریافت لیست صفحه‌بندی شده فاکتورهای فروش با قابلیت جستجو
    /// </summary>
    /// <param name="page">شماره صفحه (از 1 شروع می‌شود)</param>
    /// <param name="pageSize">تعداد آیتم در هر صفحه</param>
    /// <param name="search">عبارت جستجو (اختیاری)</param>
    /// <returns>نتیجه صفحه‌بندی شده شامل لیست فاکتورها و اطلاعات صفحه</returns>
    public async Task<PagedResult<SalesInvoiceSummaryDto>> GetInvoicesPagedAsync(int page, int pageSize, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _unitOfWork.SalesInvoices.GetPagedAsync(page, pageSize, search);

        return new PagedResult<SalesInvoiceSummaryDto>
        {
            Items = items.Select(i => new SalesInvoiceSummaryDto(
                i.Id, i.InvoiceNumber, i.InvoiceDate, i.Customer?.Name, i.Warehouse?.Name ?? "-",
                i.Status.ToString(), i.TotalAmount)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}




