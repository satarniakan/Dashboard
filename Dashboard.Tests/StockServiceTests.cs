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

public class StockServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IStockLevelRepository> _stockLevels = new();
    private readonly Mock<IStockCountRepository> _stockCounts = new();
    private readonly Mock<IStockValidator> _stockValidator = new();
    private readonly StockService _sut;

    public StockServiceTests()
    {
        _unitOfWork.Setup(u => u.StockLevels).Returns(_stockLevels.Object);
        _unitOfWork.Setup(u => u.StockCounts).Returns(_stockCounts.Object);
        _unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        _sut = new StockService(_unitOfWork.Object, Mock.Of<ILogger<StockService>>(), Mock.Of<IJournalService>(), _stockValidator.Object);
    }

    [Fact]
    public async Task RegisterStockTransferAsync_WithNoItems_ThrowsBusinessRuleException()
    {
        var dto = new CreateStockTransferDto { SourceWarehouseId = 1, DestinationWarehouseId = 2, Items = new() };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterStockTransferAsync(dto, "user1"));
    }

    [Fact]
    public async Task RegisterStockTransferAsync_WhenSourceEqualsDestination_ThrowsBusinessRuleException()
    {
        var dto = new CreateStockTransferDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 1, // Ù‡Ù…ÙˆÙ† Ø§Ù†Ø¨Ø§Ø± Ù…Ø¨Ø¯Ø§!
            Items = new() { new StockItemInput { ProductId = 1, Quantity = 5 } }
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterStockTransferAsync(dto, "user1"));
    }

    [Fact]
    public async Task RegisterStockTransferAsync_WhenSourceStockIsInsufficient_ThrowsBusinessRuleException()
    {
        var dto = new CreateStockTransferDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 2,
            Items = new() { new StockItemInput { ProductId = 10, Quantity = 5 } }
        };
        // ÙÙ‚Ø· Û³ Ø¹Ø¯Ø¯ ØªÙˆ Ø§Ù†Ø¨Ø§Ø± Ù…Ø¨Ø¯Ø§ Ù…ÙˆØ¬ÙˆØ¯Ù‡ØŒ Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ûµ ØªØ§Ø³Øª
        _stockValidator.Setup(v => v.ValidateSufficientStockAsync(1, dto.Items))
                    .ThrowsAsync(new BusinessRuleException("موجودی کافی نیست."));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterStockTransferAsync(dto, "user1"));
    }

    [Fact]
    public async Task CloseStockCountAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _stockCounts.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((StockCount?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.CloseStockCountAsync(99, new Dictionary<int, decimal>(), "user1"));
    }

    [Fact]
    public async Task CloseStockCountAsync_WhenAlreadyClosed_ThrowsBusinessRuleException()
    {
        var stockCount = new StockCount { Id = 1, Status = StockCountStatus.Closed };
        _stockCounts.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(stockCount);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.CloseStockCountAsync(1, new Dictionary<int, decimal>(), "user1"));
    }
}

