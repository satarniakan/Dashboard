using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface ISalesService
{
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto);
    Task<IEnumerable<CustomerDto>> GetCustomersAsync();

    Task<int> CreateDraftInvoiceAsync(CreateSalesInvoiceDto dto, string? userId);
    Task<SalesInvoiceDto?> GetInvoiceAsync(int id);
    Task<IEnumerable<SalesInvoiceSummaryDto>> GetInvoicesAsync();

    Task ConfirmInvoiceAsync(int invoiceId, string? userId);
    Task CancelInvoiceAsync(int invoiceId, string? userId);
}

public class SalesService : ISalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalesService> _logger;

    public SalesService(IUnitOfWork unitOfWork, ILogger<SalesService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---------------- مشتریان ----------------

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto)
    {
        var customer = new Customer(dto.Name, dto.Phone, dto.Address);
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.CompleteAsync();
        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Address);
    }

    public async Task<IEnumerable<CustomerDto>> GetCustomersAsync()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        return customers.Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Address));
    }

    // ---------------- ساخت پیش‌نویس فاکتور ----------------

    public async Task<int> CreateDraftInvoiceAsync(CreateSalesInvoiceDto dto, string? userId)
    {
        if (dto.Items.Count == 0) throw new InvalidOperationException("حداقل یک قلم کالا لازم است.");

        var invoice = new SalesInvoice
        {
            CustomerId = dto.CustomerId,
            WarehouseId = dto.WarehouseId,
            InvoiceNumber = dto.InvoiceNumber,
            InvoiceDate = dto.InvoiceDate,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Status = SalesInvoiceStatus.Draft,
            CreatedByUserId = userId
        };

        decimal total = 0;
        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("مقدار باید بزرگتر از صفر باشد.");
            if (item.UnitPrice < 0) throw new InvalidOperationException("قیمت واحد نمی‌تواند منفی باشد.");

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
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesInvoiceDrafted", userId, $"فاکتور {dto.InvoiceNumber} به‌صورت پیش‌نویس ثبت شد."));
        await _unitOfWork.CompleteAsync();

        return invoice.Id;
    }

    // ---------------- تأیید فاکتور (اینجا موجودی واقعاً کسر می‌شود) ----------------

    public async Task ConfirmInvoiceAsync(int invoiceId, string? userId)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException("فاکتور یافت نشد.");

        if (invoice.Status != SalesInvoiceStatus.Draft)
            throw new InvalidOperationException("فقط فاکتور پیش‌نویس قابل تأیید است.");

        // اول همه اقلام را چک می‌کنیم؛ اگر یکی کم بود، هیچ‌کدام کسر نمی‌شود
        foreach (var item in invoice.Items)
        {
            var level = await _unitOfWork.StockLevels.GetAsync(item.ProductId, invoice.WarehouseId);
            var available = level?.QuantityOnHand ?? 0;

            if (available < item.Quantity)
                throw new InvalidOperationException(
                    $"موجودی کافی نیست (کالای «{item.Product?.Name}»: موجود {available}, درخواستی {item.Quantity}).");
        }

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

            await _unitOfWork.StockLevels.IncreaseOrCreateAsync(item.ProductId, invoice.WarehouseId, -item.Quantity);
        }

        invoice.Status = SalesInvoiceStatus.Confirmed;
        invoice.ConfirmedAt = DateTime.UtcNow;

        await _unitOfWork.SalesInvoices.UpdateAsync(invoice);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SalesInvoiceConfirmed", userId, $"فاکتور {invoice.InvoiceNumber} تأیید و موجودی کسر شد."));
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("Sales invoice {InvoiceNumber} confirmed by {UserId}", invoice.InvoiceNumber, userId);
    }

    // ---------------- لغو فاکتور (برگشت موجودی) ----------------

    public async Task CancelInvoiceAsync(int invoiceId, string? userId)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException("فاکتور یافت نشد.");

        if (invoice.Status == SalesInvoiceStatus.Canceled)
            throw new InvalidOperationException("این فاکتور قبلاً لغو شده است.");

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
    }

    // ---------------- خواندن ----------------

    public async Task<SalesInvoiceDto?> GetInvoiceAsync(int id)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(id);
        if (invoice is null) return null;

        return new SalesInvoiceDto(
            invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
            invoice.Customer?.Name, invoice.Warehouse?.Name ?? "-", invoice.Status.ToString(),
            invoice.DiscountAmount, invoice.TotalAmount, invoice.Notes,
            invoice.Items.Select(i => new SalesInvoiceItemDto(
                i.ProductId, i.Product?.Name ?? "-", i.Quantity, i.UnitPrice, i.LineTotal)).ToList());
    }

    public async Task<IEnumerable<SalesInvoiceSummaryDto>> GetInvoicesAsync()
    {
        var invoices = await _unitOfWork.SalesInvoices.GetAllAsync();
        return invoices.Select(i => new SalesInvoiceSummaryDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.Customer?.Name, i.Warehouse?.Name ?? "-",
            i.Status.ToString(), i.TotalAmount));
    }
}