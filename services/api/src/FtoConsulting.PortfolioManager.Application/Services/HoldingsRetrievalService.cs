using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for retrieving holdings data with related entities
/// Supports both historical data retrieval and real-time pricing for current date
/// </summary>
public class HoldingsRetrievalService : IHoldingsRetrieval
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly ILogger<HoldingsRetrievalService> _logger;
    private readonly Func<EodMarketDataTool>? _eodMarketDataToolFactory;
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly IPricingCalculationService _pricingCalculationService;

    public HoldingsRetrievalService(
        IHoldingRepository holdingRepository,
        ILogger<HoldingsRetrievalService> logger,
        ICurrencyConversionService currencyConversionService,
        IPricingCalculationService pricingCalculationService,
        Func<EodMarketDataTool>? eodMarketDataToolFactory = null)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currencyConversionService = currencyConversionService ?? throw new ArgumentNullException(nameof(currencyConversionService));
        _pricingCalculationService = pricingCalculationService ?? throw new ArgumentNullException(nameof(pricingCalculationService));
        _eodMarketDataToolFactory = eodMarketDataToolFactory;
    }

    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            IEnumerable<Holding> holdings;

            if (valuationDate < today)
            {
                // Historical data - get holdings for the specific date
                var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
                _logger.LogInformation("Retrieved {Count} historical holdings for account {AccountId} on date {ValuationDate}", 
                    holdings.Count(), accountId, valuationDate);
            }
            else if (valuationDate == today)
            {
                // Real-time data - get the most recent holdings and apply real-time pricing
                var latestDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
                if (latestDate.HasValue)
                {
                    // Get ALL holdings from the latest date, then filter by account
                    var allLatestHoldings = await _holdingRepository.GetHoldingsByValuationDateWithInstrumentsAsync(latestDate.Value, cancellationToken);
                    holdings = allLatestHoldings.Where(h => h.Portfolio.AccountId == accountId).ToList();
                    _logger.LogInformation("Retrieved {Count} latest holdings for account {AccountId} from date {LatestDate} for real-time pricing", 
                        holdings.Count(), accountId, latestDate.Value);

                    if (_eodMarketDataToolFactory != null)
                    {
                        _logger.LogInformation("Applying real-time pricing for today's date: {Date}", valuationDate);
                        holdings = await ApplyRealTimePricing(holdings.ToList(), valuationDate, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("EOD market data tool not available for real-time pricing, returning latest holdings with historical prices");
                    }
                }
                else
                {
                    _logger.LogWarning("No historical holdings found for account {AccountId}, returning empty holdings", accountId);
                    holdings = Enumerable.Empty<Holding>();
                }
            }
            else
            {
                // Future date - not supported, return empty
                _logger.LogWarning("Future date {ValuationDate} requested, returning empty holdings", valuationDate);
                holdings = Enumerable.Empty<Holding>();
            }
            
            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            throw;
        }
    }

    /// <summary>
    /// Apply real-time pricing to holdings using EOD market data (no persistence)
    /// </summary>
    private async Task<IEnumerable<Holding>> ApplyRealTimePricing(List<Holding> holdings, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        try
        {
            if (_eodMarketDataToolFactory == null)
            {
                _logger.LogWarning("EOD market data tool not available for real-time pricing");
                return holdings;
            }

            // Extract tickers from holdings (excluding CASH)
            var tickers = holdings
                .Where(h => !string.IsNullOrEmpty(h.Instrument?.Ticker))
                .Where(h => !h.Instrument!.Ticker!.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Instrument!.Ticker!)
                .Distinct()
                .ToList();

            // Fetch real-time prices if we have tickers
            Dictionary<string, decimal> prices = new Dictionary<string, decimal>();
            if (tickers.Any())
            {
                var eodTool = _eodMarketDataToolFactory();
                prices = await eodTool.GetRealTimePricesAsync(null, tickers, cancellationToken);

                _logger.LogInformation("Fetched {Count} real-time prices for {TotalTickers} tickers", 
                    prices.Count, tickers.Count);

            }

            // Apply real-time prices to ALL holdings (including those without real-time prices)
            var updatedCount = 0;
            foreach (var holding in holdings)
            {
                // Handle CASH holdings - update date but keep same value with zero daily P&L
                if (holding.Instrument?.Ticker?.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase) == true)
                {
                    holding.SetDailyProfitLoss(0m, 0m);
                    holding.UpdateValuation(DateTime.UtcNow, holding.CurrentValue);
                    
                    _logger.LogInformation("Updated CASH holding {HoldingId} for today - keeping value {CurrentValue:C}, setting daily P/L to zero", 
                        holding.Id, holding.CurrentValue);
                    continue;
                }

                // Handle holdings with real-time prices available
                if (holding.Instrument?.Ticker != null && prices.TryGetValue(holding.Instrument.Ticker, out var realTimePrice))
                {
                    var originalValue = holding.CurrentValue;
                    
                    // Apply scaling factor for proxy instruments using shared service
                    var scaledPrice = _pricingCalculationService.ApplyScalingFactor(realTimePrice, holding.Instrument.Ticker);
                    
                    // Use the shared pricing calculation service
                    var newCurrentValue = await _pricingCalculationService.CalculateCurrentValueAsync(
                        holding.UnitAmount, 
                        scaledPrice, // Use scaled price instead of raw price
                        holding.Instrument.QuoteUnit,
                        _pricingCalculationService.GetCurrencyFromTicker(holding.Instrument.Ticker),
                        DateOnly.FromDateTime(DateTime.UtcNow));
                    
                    // Calculate daily P&L based on the change from previous value to new real-time value
                    var dailyChange = newCurrentValue - originalValue;
                    var dailyChangePercentage = originalValue != 0 
                        ? (dailyChange / originalValue) * 100 
                        : 0;
                    
                    // Update both current value and valuation date to today for real-time pricing
                    holding.UpdateValuation(DateTime.UtcNow, newCurrentValue);
                    
                    // Set the recalculated daily profit/loss
                    holding.SetDailyProfitLoss(dailyChange, dailyChangePercentage);
                    
                    updatedCount++;
                    
                    _logger.LogInformation("Updated holding {HoldingId} for {Ticker}: Original value {OriginalValue:C} -> New value {NewValue:C}, Daily P/L: {DailyChange:C} ({DailyChangePercentage:F2}%) (Real-time price: {RealTimePrice}, Scaled price: {ScaledPrice}, Quote unit: {QuoteUnit})", 
                        holding.Id, holding.Instrument.Ticker, originalValue, newCurrentValue, dailyChange, dailyChangePercentage, realTimePrice, scaledPrice, holding.Instrument.QuoteUnit ?? CurrencyConstants.DEFAULT_QUOTE_UNIT);
                }

            }

            _logger.LogInformation("Successfully updated {UpdatedCount} holdings with real-time pricing, {UnchangedCount} holdings kept with original dates", 
                updatedCount, holdings.Count - updatedCount);

            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time pricing, returning holdings with original prices");
            return holdings;
        }
    }
}