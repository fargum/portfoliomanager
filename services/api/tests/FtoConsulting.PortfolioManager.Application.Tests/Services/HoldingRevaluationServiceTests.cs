using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services;

public class HoldingRevaluationServiceTests
{
    private readonly Mock<IHoldingRepository> _holdingRepositoryMock;
    private readonly Mock<IInstrumentPriceRepository> _instrumentPriceRepositoryMock;
    private readonly Mock<ICurrencyConversionService> _currencyConversionServiceMock;
    private readonly Mock<IPriceFetching> _priceFetchingMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<HoldingRevaluationService>> _loggerMock;
    private readonly HoldingRevaluationService _service;

    public HoldingRevaluationServiceTests()
    {
        _holdingRepositoryMock = new Mock<IHoldingRepository>();
        _instrumentPriceRepositoryMock = new Mock<IInstrumentPriceRepository>();
        _currencyConversionServiceMock = new Mock<ICurrencyConversionService>();
        _priceFetchingMock = new Mock<IPriceFetching>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<HoldingRevaluationService>>();
        
        // Setup default currency conversion behavior (GBP to GBP = no conversion)
        _currencyConversionServiceMock
            .Setup(x => x.ConvertCurrencyAsync(It.IsAny<decimal>(), "GBP", "GBP", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal amount, string from, string to, DateOnly date, CancellationToken ct) => (amount, 1m, "SAME_CURRENCY"));
        
        _service = new HoldingRevaluationService(
            _holdingRepositoryMock.Object,
            _instrumentPriceRepositoryMock.Object,
            _currencyConversionServiceMock.Object,
            _priceFetchingMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RevalueHoldingsAsync_WithNoExistingHoldings_ReturnsEmptyResult()
    {
        // Arrange
        var valuationDate = DateOnly.FromDateTime(DateTime.Today);
        
        _holdingRepositoryMock
            .Setup(x => x.GetLatestValuationDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly?)null);

        // Act
        var result = await _service.RevalueHoldingsAsync(valuationDate);

        // Assert
        Assert.Equal(valuationDate, result.ValuationDate);
        Assert.Equal(0, result.TotalHoldings);
        Assert.Equal(0, result.SuccessfulRevaluations);
        Assert.Equal(0, result.FailedRevaluations);
        Assert.Null(result.SourceValuationDate);
    }

    [Fact]
    public async Task RevalueHoldingsAsync_WithGBXQuoteUnit_ConvertsToGBP()
    {
        // Arrange
        var valuationDate = DateOnly.FromDateTime(DateTime.Today);
        var sourceDate = valuationDate.AddDays(-1);
        
        var instrumentTypeId = 1;
        var instrumentId = 1;
        var portfolioId = 1;
        var platformId = 1;

        var instrument = new Instrument("Apple Inc", "AAPL", instrumentTypeId, "Technology company", "USD", "GBX");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(instrument, instrumentId);

        var sourceHolding = new Holding(sourceDate.ToDateTime(TimeOnly.MinValue), instrumentId, platformId, portfolioId, 100, 15000m, 18000m);
        typeof(Holding).GetProperty("Instrument")!.SetValue(sourceHolding, instrument);

        var instrumentPrice = new InstrumentPrice
        {
            Ticker = "AAPL",
            ValuationDate = valuationDate,
            Price = 12000m, // 120.00 pounds in pence
            Currency = "GBX"
        };

        _holdingRepositoryMock
            .Setup(x => x.GetLatestValuationDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceDate);

        _holdingRepositoryMock
            .Setup(x => x.GetHoldingsByValuationDateWithInstrumentsAsync(sourceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceHolding });

        _holdingRepositoryMock
            .Setup(x => x.GetHoldingsByValuationDateWithInstrumentsAsync(valuationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Holding>());

        _instrumentPriceRepositoryMock
            .Setup(x => x.GetByValuationDateAsync(valuationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { instrumentPrice });

        // Act
        var result = await _service.RevalueHoldingsAsync(valuationDate);

        // Assert
        Assert.Equal(1, result.SuccessfulRevaluations);
        Assert.Equal(0, result.FailedRevaluations);
        Assert.Equal(sourceDate, result.SourceValuationDate);

        // Verify that AddAsync was called with a holding that has the correct current value
        // 100 units * 120.00 GBP (12000 pence / 100) = 12,000 GBP
        _holdingRepositoryMock.Verify(x => x.AddAsync(It.Is<Holding>(h => h.CurrentValue == 12000m)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task FetchPricesAndRevalueHoldingsAsync_PerformsBothOperations_ReturnsCompleteResult()
    {
        // Arrange
        var valuationDate = new DateOnly(2023, 11, 20);
        var sourceDate = new DateOnly(2023, 11, 19);
        
        var instrumentTypeId = 1;
        var instrumentId = 1;
        var portfolioId = 1;
        var platformId = 1;

        var instrument = new Instrument("Test Corp", "TEST.LSE", instrumentTypeId, "Test company", "GBP", "GBP");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(instrument, instrumentId);

        var sourceHolding = new Holding(sourceDate.ToDateTime(TimeOnly.MinValue), instrumentId, platformId, portfolioId, 100, 10000m, 10000m);
        typeof(Holding).GetProperty("Instrument")!.SetValue(sourceHolding, instrument);

        var instrumentPrice = new InstrumentPrice
        {
            Ticker = "TEST.LSE",
            Price = 105m,
            Currency = "GBP",
            ValuationDate = valuationDate
        };

        // Setup price fetching mock
        var priceFetchResult = new Application.Models.PriceFetchResult
        {
            TotalTickers = 1,
            SuccessfulPrices = 1,
            FetchDuration = TimeSpan.FromSeconds(2),
            FetchedAt = DateTime.UtcNow
        };

        _priceFetchingMock
            .Setup(x => x.FetchAndPersistPricesForDateAsync(valuationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceFetchResult);

        // Setup existing revaluation mocks
        _holdingRepositoryMock
            .Setup(x => x.GetLatestValuationDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceDate);

        _holdingRepositoryMock
            .Setup(x => x.GetHoldingsByValuationDateWithInstrumentsAsync(sourceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceHolding });

        _holdingRepositoryMock
            .Setup(x => x.GetHoldingsByValuationDateWithInstrumentsAsync(valuationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Holding>());

        _instrumentPriceRepositoryMock
            .Setup(x => x.GetByValuationDateAsync(valuationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { instrumentPrice });

        // Act
        var result = await _service.FetchPricesAndRevalueHoldingsAsync(valuationDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(valuationDate, result.ValuationDate);
        Assert.True(result.OverallSuccess);
        Assert.Equal(1, result.PriceFetchResult.SuccessfulPrices);
        Assert.Equal(1, result.HoldingRevaluationResult.SuccessfulRevaluations);
        Assert.Contains("1/1 prices", result.Summary);
        Assert.Contains("1/1 holdings", result.Summary);

        // Verify both operations were called
        _priceFetchingMock.Verify(x => x.FetchAndPersistPricesForDateAsync(valuationDate, It.IsAny<CancellationToken>()), Times.Once);
        _holdingRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Holding>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}