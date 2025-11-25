using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services;

public class PortfolioIngestServiceTests
{
    private readonly Mock<IPortfolioRepository> _mockPortfolioRepository;
    private readonly Mock<IInstrumentRepository> _mockInstrumentRepository;
    private readonly Mock<IHoldingRepository> _mockHoldingRepository;
    private readonly Mock<IInstrumentManagementService> _mockInstrumentManagementService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<PortfolioIngestService>> _mockLogger;
    private readonly PortfolioIngestService _service;

    public PortfolioIngestServiceTests()
    {
        _mockPortfolioRepository = new Mock<IPortfolioRepository>();
        _mockInstrumentRepository = new Mock<IInstrumentRepository>();
        _mockHoldingRepository = new Mock<IHoldingRepository>();
        _mockInstrumentManagementService = new Mock<IInstrumentManagementService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<PortfolioIngestService>>();

        _service = new PortfolioIngestService(
            _mockPortfolioRepository.Object,
            _mockInstrumentRepository.Object,
            _mockHoldingRepository.Object,
            _mockInstrumentManagementService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IngestPortfolioAsync_WithNewInstrument_CreatesInstrumentAndHolding()
    {
        // Arrange
        var instrumentId = 1;
        var portfolioId = 1;
        var accountId = 1;
        var holdingId = 1;
        var platformId = 1;
        var instrumentTypeId = 1;

        var instrument = new Instrument("Apple Inc", "AAPL", instrumentTypeId, "Technology company", "USD", "USD");
        // Use reflection to set the Id for testing purposes
        typeof(BaseEntity).GetProperty("Id")!.SetValue(instrument, instrumentId);

        var holding = new Holding(DateTime.Today, instrumentId, platformId, portfolioId, 100, 15000m, 18000m);
        // Use reflection to set the Id and instrument navigation property for testing
        typeof(BaseEntity).GetProperty("Id")!.SetValue(holding, holdingId);
        typeof(Holding).GetProperty("Instrument")!.SetValue(holding, instrument);

        var portfolio = new Portfolio("Test Portfolio", accountId);
        // Use reflection to set the Id for testing purposes
        typeof(BaseEntity).GetProperty("Id")!.SetValue(portfolio, portfolioId);
        portfolio.AddHolding(holding);

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(portfolioId))
            .ReturnsAsync((Portfolio?)null);
        
        // Mock the InstrumentManagementService to return the instrument when called
        _mockInstrumentManagementService.Setup(x => x.EnsureInstrumentExistsAsync(It.IsAny<Instrument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        
        _mockInstrumentRepository.SetupSequence(x => x.GetByTickerAsync("AAPL"))
            .ReturnsAsync((Instrument?)null)  // First call - instrument doesn't exist
            .ReturnsAsync(instrument);        // Second call - return the instrument after it's been "added"

        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portfolio>()))
            .ReturnsAsync(portfolio);

        _mockPortfolioRepository.Setup(x => x.GetWithHoldingsAsync(portfolioId))
            .ReturnsAsync(portfolio);

        // Act
        var result = await _service.IngestPortfolioAsync(portfolio);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(portfolioId, result.Id);
        
        // Verify that InstrumentManagementService was called instead of direct repository calls
        _mockInstrumentManagementService.Verify(x => x.EnsureInstrumentExistsAsync(It.IsAny<Instrument>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockHoldingRepository.Verify(x => x.AddAsync(It.IsAny<Holding>()), Times.Once);
        _mockPortfolioRepository.Verify(x => x.AddAsync(It.IsAny<Portfolio>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestPortfolioAsync_WithExistingInstrument_UsesExistingInstrument()
    {
        // Arrange
        var instrumentId = 2;
        var portfolioId = 2;
        var accountId = 2;
        var holdingId = 2;
        var platformId = 2;
        var instrumentTypeId = 2;

        var existingInstrument = new Instrument("Apple Inc", "AAPL", instrumentTypeId, "Technology company", "USD", "USD");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(existingInstrument, instrumentId);

        var newInstrument = new Instrument("Apple Inc Updated", "AAPL", instrumentTypeId, "Updated description", "USD", "GBP");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(newInstrument, 3);

        var holding = new Holding(DateTime.Today, newInstrument.Id, platformId, portfolioId, 100, 15000m, 18000m);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(holding, holdingId);
        typeof(Holding).GetProperty("Instrument")!.SetValue(holding, newInstrument);

        var portfolio = new Portfolio("Test Portfolio", accountId);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(portfolio, portfolioId);
        portfolio.AddHolding(holding);

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(portfolioId))
            .ReturnsAsync((Portfolio?)null);
        
        // Mock the InstrumentManagementService to return the existing instrument
        _mockInstrumentManagementService.Setup(x => x.EnsureInstrumentExistsAsync(It.IsAny<Instrument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInstrument);
        
        _mockInstrumentRepository.Setup(x => x.GetByTickerAsync("AAPL"))
            .ReturnsAsync(existingInstrument);

        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portfolio>()))
            .ReturnsAsync(portfolio);

        _mockPortfolioRepository.Setup(x => x.GetWithHoldingsAsync(portfolioId))
            .ReturnsAsync(portfolio);

        // Act
        var result = await _service.IngestPortfolioAsync(portfolio);

        // Assert
        Assert.NotNull(result);
        
        // Should update existing instrument, not create new one
        _mockInstrumentRepository.Verify(x => x.AddAsync(It.IsAny<Instrument>()), Times.Never);
        _mockInstrumentRepository.Verify(x => x.UpdateAsync(It.IsAny<Instrument>()), Times.Once);
        _mockHoldingRepository.Verify(x => x.AddAsync(It.IsAny<Holding>()), Times.Once);
    }

    [Fact]
    public async Task IngestPortfolioAsync_WithNullPortfolio_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.IngestPortfolioAsync(null!));
    }

    [Fact]
    public async Task IngestPortfolioAsync_WhenExceptionOccurs_RollsBackTransaction()
    {
        // Arrange
        var portfolio = new Portfolio("Test Portfolio", 1);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(portfolio, 1);

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            _service.IngestPortfolioAsync(portfolio));

        _mockUnitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }
}