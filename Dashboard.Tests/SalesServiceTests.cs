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
    // به‌جای دیتابیس واقعی، هر انباردار (Repository) را با Moq جعلی می‌سازیم
    // تا سرویس را کاملاً مستقل و سریع تست کنیم.
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISalesInvoiceRepository> _salesInvoices = new();
    private readonly Mock<IStockLevelRepository> _stockLevels = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly SalesService _sut; // Sut = System Under Test، یعنی «چیزی که داریم تستش می‌کنیم»

    public SalesServiceTests()
    {
        _unitOfWork.Setup(u => u.SalesInvoices).Returns(_salesInvoices.Object);
        _unitOfWork.Setup(u => u.StockLevels).Returns(_stockLevels.Object);
        _unitOfWork.Setup(u => u.AuditLogs).Returns(_auditLogs.Object);
        _unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        _sut = new SalesService(_unitOfWork.Object, Mock.Of<IJournalService>(), Mock.Of<ILogger<SalesService>>());
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
                inv.Id = 42; // شبیه‌سازی رفتار دیتابیس واقعی بعد از SaveChanges
                savedInvoice = inv;
            })
            .Returns(Task.CompletedTask);

        var invoiceId = await _sut.CreateDraftInvoiceAsync(dto, "user1");

        Assert.Equal(42, invoiceId);
        Assert.NotNull(savedInvoice);
        // جمع اقلام (200 + 200) منهای تخفیف (50) = 350
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
                new() { ProductId = 10, Quantity = 5, Product = new Product("کالای تستی", 100) }
            }
        };
        _salesInvoices.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(invoice);
        // فقط ۲ عدد موجوده ولی فاکتور ۵ تا خواسته
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
