using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for real-time portfolio valuation using live market data
/// </summary>
public class RealTimePortfolioService : IRealTimePortfolioService
{
    private readonly ILogger<RealTimePortfolioService> _logger;
    private readonly IHoldingRepository _holdingRepository;
    private readonly IMcpServerService? _mcpServerService;
    private readonly Func<EodMarketDataTool>? _eodMarketDataToolFactory;

    public RealTimePortfolioService(
        ILogger<RealTimePortfolioService> logger,
        IHoldingRepository holdingRepository,
        IMcpServerService? mcpServerService = null,
        Func<EodMarketDataTool>? eodMarketDataToolFactory = null)
    {
        _logger = logger;
        _holdingRepository = holdingRepository;
        _mcpServerService = mcpServerService;
        _eodMarketDataToolFactory = eodMarketDataToolFactory;
    }

    public async Task<RealTimePortfolioDto> GetRealTimePortfolioAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting real-time portfolio for account {AccountId}", accountId);

            // Get the latest holdings data
            var latestDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (!latestDate.HasValue)
            {
                _logger.LogWarning("No holdings data found for account {AccountId}", accountId);
                return CreateEmptyPortfolio(accountId);
            }

            var holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, latestDate.Value.ToDateTime(TimeOnly.MinValue), cancellationToken);
            if (!holdings.Any())
            {
                _logger.LogWarning("No holdings found for account {AccountId} on date {Date}", accountId, latestDate);
                return CreateEmptyPortfolio(accountId);
            }

            _logger.LogInformation("Found {Count} holdings for account {AccountId} on {Date}", 
                holdings.Count(), accountId, latestDate);

            // Get all unique tickers for price fetching
            var tickers = holdings.Where(h => h.Instrument != null)
                                 .Select(h => h.Instrument!.Ticker)
                                 .Distinct()
                                 .ToList();

            _logger.LogInformation("Fetching real-time prices for {Count} tickers: {Tickers}", 
                tickers.Count, string.Join(", ", tickers));

            // Fetch real-time prices
            var realTimePrices = await FetchRealTimePricesAsync(tickers, cancellationToken);

            _logger.LogInformation("Retrieved {Count} real-time prices out of {Total} tickers", 
                realTimePrices.Count, tickers.Count);

            // Create real-time holdings
            var priceTimestamp = DateTime.UtcNow;
            var realTimeHoldings = holdings.Where(h => h.Instrument != null)
                                          .Select(h => CreateRealTimeHolding(h, realTimePrices, priceTimestamp))
                                          .ToList();

            // Calculate portfolio totals
            var totalValue = realTimeHoldings.Sum(h => h.CurrentValue);
            var totalCost = realTimeHoldings.Sum(h => h.CostBasis);
            var unrealizedGainLoss = totalValue - totalCost;
            var unrealizedGainLossPercentage = totalCost != 0 ? (unrealizedGainLoss / totalCost) * 100 : 0;

            var holdingsWithRealTimePrices = realTimeHoldings.Count(h => h.HasRealTimePrice);

            var portfolio = new RealTimePortfolioDto(
                AccountId: accountId,
                LatestHoldingsDate: latestDate.Value,
                PriceTimestamp: priceTimestamp,
                TotalValue: totalValue,
                TotalCost: totalCost,
                UnrealizedGainLoss: unrealizedGainLoss,
                UnrealizedGainLossPercentage: unrealizedGainLossPercentage,
                TotalHoldings: realTimeHoldings.Count,
                HoldingsWithRealTimePrices: holdingsWithRealTimePrices,
                Holdings: realTimeHoldings
            );

            _logger.LogInformation("Real-time portfolio calculated for account {AccountId}: Value={TotalValue:C}, " +
                                 "Gain/Loss={UnrealizedGainLoss:C} ({UnrealizedGainLossPercentage:F2}%), " +
                                 "Real-time prices: {RealTimePrices}/{Total}",
                accountId, totalValue, unrealizedGainLoss, unrealizedGainLossPercentage, 
                holdingsWithRealTimePrices, realTimeHoldings.Count);

            return portfolio;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting real-time portfolio for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<IEnumerable<RealTimeHoldingDto>> GetRealTimeHoldingsAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var portfolio = await GetRealTimePortfolioAsync(accountId, cancellationToken);
        return portfolio.Holdings;
    }

    /// <summary>
    /// Fetch real-time prices from EOD Historical Data
    /// </summary>
    private async Task<Dictionary<string, decimal>> FetchRealTimePricesAsync(
        IEnumerable<string> tickers, 
        CancellationToken cancellationToken)
    {
        if (_eodMarketDataToolFactory == null || _mcpServerService == null)
        {
            _logger.LogWarning("EOD market data service not configured, returning empty prices");
            return new Dictionary<string, decimal>();
        }

        try
        {
            var eodMarketDataTool = _eodMarketDataToolFactory();
            var prices = await eodMarketDataTool.GetRealTimePricesAsync(_mcpServerService, tickers, cancellationToken);
            
            _logger.LogInformation("Successfully fetched {Count} real-time prices from EOD", prices.Count);
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching real-time prices from EOD");
            return new Dictionary<string, decimal>();
        }
    }

    /// <summary>
    /// Create real-time holding DTO from domain holding and price data
    /// </summary>
    private RealTimeHoldingDto CreateRealTimeHolding(
        Domain.Entities.Holding holding, 
        Dictionary<string, decimal> realTimePrices, 
        DateTime priceTimestamp)
    {
        var ticker = holding.Instrument!.Ticker;
        var hasRealTimePrice = realTimePrices.TryGetValue(ticker, out var realTimePrice);
        
        // Calculate the current unit price from the stored CurrentValue and UnitAmount
        var storedUnitPrice = holding.UnitAmount != 0 ? holding.CurrentValue / holding.UnitAmount : 0;
        
        // If no real-time price available, use the calculated unit price from stored data
        var effectivePrice = hasRealTimePrice ? realTimePrice : storedUnitPrice;
        var currentValue = holding.UnitAmount * effectivePrice;
        var totalCost = holding.BoughtValue;
        var unrealizedGainLoss = currentValue - totalCost;
        var unrealizedGainLossPercentage = totalCost != 0 ? (unrealizedGainLoss / totalCost) * 100 : 0;

        return new RealTimeHoldingDto(
            HoldingId: holding.Id,
            Ticker: ticker,
            InstrumentName: holding.Instrument.Name,
            Quantity: holding.UnitAmount,
            CostBasis: holding.BoughtValue,
            RealTimePrice: hasRealTimePrice ? realTimePrice : null,
            HasRealTimePrice: hasRealTimePrice,
            CurrentValue: currentValue,
            UnrealizedGainLoss: unrealizedGainLoss,
            UnrealizedGainLossPercentage: unrealizedGainLossPercentage,
            HoldingDate: DateOnly.FromDateTime(holding.ValuationDate),
            PriceTimestamp: hasRealTimePrice ? priceTimestamp : null
        );
    }

    /// <summary>
    /// Create empty portfolio for when no holdings are found
    /// </summary>
    private RealTimePortfolioDto CreateEmptyPortfolio(int accountId)
    {
        return new RealTimePortfolioDto(
            AccountId: accountId,
            LatestHoldingsDate: DateOnly.FromDateTime(DateTime.Today),
            PriceTimestamp: DateTime.UtcNow,
            TotalValue: 0,
            TotalCost: 0,
            UnrealizedGainLoss: 0,
            UnrealizedGainLossPercentage: 0,
            TotalHoldings: 0,
            HoldingsWithRealTimePrices: 0,
            Holdings: Array.Empty<RealTimeHoldingDto>()
        );
    }
}