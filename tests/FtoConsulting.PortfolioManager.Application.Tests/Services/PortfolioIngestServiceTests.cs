using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
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

        var instrument = new Instrument
        {
            Id = instrumentId,
            ISIN = "US0378331005",
            Name = "Apple Inc",
            Description = "Technology company",
            InstrumentTypeId = instrumentTypeId,
            CreatedAt = DateTime.UtcNow
        };

        var holding = new Holding
        {
            Id = holdingId,
            InstrumentId = instrumentId,
            Instrument = instrument,
            PortfolioId = portfolioId,
            PlatformId = platformId,
            ValuationDate = DateTime.Today,
            UnitAmount = 100,
            BoughtValue = 15000m,
            CurrentValue = 18000m,
            CreatedAt = DateTime.UtcNow
        };

        var portfolio = new Portfolio
        {
            Id = portfolioId,
            Name = "Test Portfolio",
            AccountId = accountId,
            Holdings = new List<Holding> { holding },
            CreatedAt = DateTime.UtcNow
        };

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(portfolioId))
            .ReturnsAsync((Portfolio?)null);
        
        _mockInstrumentRepository.Setup(x => x.GetByISINAsync("US0378331005"))
            .ReturnsAsync((Instrument?)null);

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

        var existingInstrument = new Instrument
        {
            Id = instrumentId,
            ISIN = "US0378331005",
            Name = "Apple Inc",
            Description = "Technology company",
            InstrumentTypeId = instrumentTypeId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var newInstrument = new Instrument
        {
            Id = Guid.NewGuid(), // Different ID
            ISIN = "US0378331005", // Same ISIN
            Name = "Apple Inc Updated",
            Description = "Updated description",
            InstrumentTypeId = instrumentTypeId,
            CreatedAt = DateTime.UtcNow
        };

        var holding = new Holding
        {
            Id = holdingId,
            Instrument = newInstrument,
            PortfolioId = portfolioId,
            PlatformId = platformId,
            ValuationDate = DateTime.Today,
            UnitAmount = 100,
            BoughtValue = 15000m,
            CurrentValue = 18000m,
            CreatedAt = DateTime.UtcNow
        };

        var portfolio = new Portfolio
        {
            Id = portfolioId,
            Name = "Test Portfolio",
            AccountId = accountId,
            Holdings = new List<Holding> { holding },
            CreatedAt = DateTime.UtcNow
        };

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
        
        // The instrument ID should be updated to match the existing one
        Assert.Equal(instrumentId, newInstrument.Id);
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
        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = "Test Portfolio",
            AccountId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _mockPortfolioRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            _service.IngestPortfolioAsync(portfolio));

        _mockUnitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }
}