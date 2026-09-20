using Dashboard.Application.DTOs;
using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dashboard.Tests;

public class SalesServiceTests
{
    // Ø¨Ù‡â€ŒØ¬Ø§ÛŒ Ø¯ÛŒØªØ§Ø¨ÛŒØ³ ÙˆØ§Ù‚Ø¹ÛŒØŒ Ù‡Ø± Ø§Ù†Ø¨Ø§Ø±Ø¯Ø§Ø± (Repository) Ø±Ø§ Ø¨Ø§ Moq Ø¬Ø¹Ù„ÛŒ Ù…ÛŒâ€ŒØ³Ø§Ø²ÛŒÙ…
    // ØªØ§ Ø³Ø±ÙˆÛŒØ³ Ø±Ø§ Ú©Ø§Ù…Ù„Ø§Ù‹ Ù…Ø³ØªÙ‚Ù„ Ùˆ Ø³Ø±ÛŒØ¹ ØªØ³Øª Ú©Ù†ÛŒÙ….
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISalesInvoiceRepository> _salesInvoices = new();
    private readonly Mock<IStockLevelRepository> _stockLevels = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly SalesService _sut; // Sut = System Under TestØŒ ÛŒØ¹Ù†ÛŒ Â«Ú†ÛŒØ²ÛŒ Ú©Ù‡ Ø¯Ø§Ø±ÛŒÙ… ØªØ³ØªØ´ Ù…ÛŒâ€ŒÚ©Ù†ÛŒÙ…Â»

    public SalesServiceTests()
    {
        _unitOfWork.Setup(u => u.SalesInvoices).Returns(_salesInvoices.Object);
        _unitOfWork.Setup(u => u.StockLevels).Returns(_stockLevels.Object);
        _unitOfWork.Setup(u => u.AuditLogs).Returns(_auditLogs.Object);
        _unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        _sut = new SalesService(_unitOfWork.Object, Mock.Of<IJournalService>(), Mock.Of<ILogger<SalesService>>(), Mock.Of<IStockValidator>());
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_WithNoItems_ThrowsBusinessRuleException()
    {
        var dto = new CreateSalesInvoiceDto { WarehouseId = 1, Items = new() };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateDraftInvoiceAsync(dto, "user1"));
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_WithZeroQuantity_ThrowsBusinessRuleException()
    {
        var dto = new CreateSalesInvoiceDto
        {
            WarehouseId = 1,
            Items = new() { new SalesInvoiceItemInput { ProductId = 1, Quantity = 0, UnitPrice = 100 } }
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateDraftInvoiceAsync(dto, "user1"));
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_WithNegativePrice_ThrowsBusinessRuleException()
    {
        var dto = new CreateSalesInvoiceDto
        {
            WarehouseId = 1,
            Items = new() { new SalesInvoiceItemInput { ProductId = 1, Quantity = 2, UnitPrice = -10 } }
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateDraftInvoiceAsync(dto, "user1"));
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_WithValidItems_CalculatesTotalCorrectly()
    {
        var dto = new CreateSalesInvoiceDto
        {
            WarehouseId = 1,
            DiscountAmount = 50,
            Items = new()
            {
                new SalesInvoiceItemInput { ProductId = 1, Quantity = 2, UnitPrice = 100 }, // 200
                new SalesInvoiceItemInput { ProductId = 2, Quantity = 1, UnitPrice = 200 }  // 200
            }
        };

        SalesInvoice? savedInvoice = null;
        _salesInvoices.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>()))
            .Callback<SalesInvoice>(inv =>
            {
                inv.Id = 42; // Ø´Ø¨ÛŒÙ‡â€ŒØ³Ø§Ø²ÛŒ Ø±ÙØªØ§Ø± Ø¯ÛŒØªØ§Ø¨ÛŒØ³ ÙˆØ§Ù‚Ø¹ÛŒ Ø¨Ø¹Ø¯ Ø§Ø² SaveChanges
                savedInvoice = inv;
            })
            .Returns(Task.CompletedTask);

        var invoiceId = await _sut.CreateDraftInvoiceAsync(dto, "user1");

        Assert.Equal(42, invoiceId);
        Assert.NotNull(savedInvoice);
        // Ø¬Ù…Ø¹ Ø§Ù‚Ù„Ø§Ù… (200 + 200) Ù…Ù†Ù‡Ø§ÛŒ ØªØ®ÙÛŒÙ (50) = 350
        Assert.Equal(350, savedInvoice!.TotalAmount);
    }

    [Fact]
    public async Task ConfirmInvoiceAsync_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        _salesInvoices.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SalesInvoice?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConfirmInvoiceAsync(99, "user1"));
    }

    [Fact]
    public async Task ConfirmInvoiceAsync_WhenInvoiceIsNotDraft_ThrowsBusinessRuleException()
    {
        var invoice = new SalesInvoice { Id = 1, Status = SalesInvoiceStatus.Confirmed, WarehouseId = 1 };
        _salesInvoices.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(invoice);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.ConfirmInvoiceAsync(1, "user1"));
    }

    [Fact]
    public async Task ConfirmInvoiceAsync_WhenStockIsInsufficient_ThrowsBusinessRuleException()
    {
        var invoice = new SalesInvoice
        {
            Id = 1,
            Status = SalesInvoiceStatus.Draft,
            WarehouseId = 1,
            Items = new List<SalesInvoiceItem>
            {
                new() { ProductId = 10, Quantity = 5, Product = new Product("Ú©Ø§Ù„Ø§ÛŒ ØªØ³ØªÛŒ", 100) }
            }
        };
        _salesInvoices.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(invoice);
        // ÙÙ‚Ø· Û² Ø¹Ø¯Ø¯ Ù…ÙˆØ¬ÙˆØ¯Ù‡ ÙˆÙ„ÛŒ ÙØ§Ú©ØªÙˆØ± Ûµ ØªØ§ Ø®ÙˆØ§Ø³ØªÙ‡
        _stockLevels.Setup(r => r.GetAsync(10, 1)).ReturnsAsync(new StockLevel { QuantityOnHand = 2 });

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.ConfirmInvoiceAsync(1, "user1"));
    }

    [Fact]
    public async Task CancelInvoiceAsync_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        _salesInvoices.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SalesInvoice?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CancelInvoiceAsync(99, "user1"));
    }

    [Fact]
    public async Task CancelInvoiceAsync_WhenAlreadyCanceled_ThrowsBusinessRuleException()
    {
        var invoice = new SalesInvoice { Id = 1, Status = SalesInvoiceStatus.Canceled };
        _salesInvoices.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(invoice);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CancelInvoiceAsync(1, "user1"));
    }
}

