using FtoConsulting.PortfolioManager.Application.Services;
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
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<PortfolioIngestService>> _mockLogger;
    private readonly PortfolioIngestService _service;

    public PortfolioIngestServiceTests()
    {
        _mockPortfolioRepository = new Mock<IPortfolioRepository>();
        _mockInstrumentRepository = new Mock<IInstrumentRepository>();
        _mockHoldingRepository = new Mock<IHoldingRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<PortfolioIngestService>>();

        _service = new PortfolioIngestService(
            _mockPortfolioRepository.Object,
            _mockInstrumentRepository.Object,
            _mockHoldingRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IngestPortfolioAsync_WithNewInstrument_CreatesInstrumentAndHolding()
    {
        // Arrange
        var instrumentId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var holdingId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var instrumentTypeId = Guid.NewGuid();

        var instrument = new Instrument("US0378331005", "Apple Inc", instrumentTypeId, null, "Technology company");
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
        
        _mockInstrumentRepository.SetupSequence(x => x.GetByISINAsync("US0378331005"))
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
        
        _mockInstrumentRepository.Verify(x => x.AddAsync(It.IsAny<Instrument>()), Times.Once);
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
        var instrumentId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var holdingId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var instrumentTypeId = Guid.NewGuid();

        var existingInstrument = new Instrument("US0378331005", "Apple Inc", instrumentTypeId, null, "Technology company");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(existingInstrument, instrumentId);

        var newInstrument = new Instrument("US0378331005", "Apple Inc Updated", instrumentTypeId, null, "Updated description");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(newInstrument, Guid.NewGuid());

        var holding = new Holding(DateTime.Today, newInstrument.Id, platformId, portfolioId, 100, 15000m, 18000m);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(holding, holdingId);
        typeof(Holding).GetProperty("Instrument")!.SetValue(holding, newInstrument);

        var portfolio = new Portfolio("Test Portfolio", accountId);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(portfolio, portfolioId);
        portfolio.AddHolding(holding);

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(portfolioId))
            .ReturnsAsync((Portfolio?)null);
        
        _mockInstrumentRepository.Setup(x => x.GetByISINAsync("US0378331005"))
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
        var portfolio = new Portfolio("Test Portfolio", Guid.NewGuid());
        typeof(BaseEntity).GetProperty("Id")!.SetValue(portfolio, Guid.NewGuid());

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            _service.IngestPortfolioAsync(portfolio));

        _mockUnitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }
}