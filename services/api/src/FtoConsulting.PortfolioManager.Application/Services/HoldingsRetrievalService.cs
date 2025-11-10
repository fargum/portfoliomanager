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
                    var latestDateTime = DateTime.SpecifyKind(latestDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                    holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, latestDateTime, cancellationToken);
                    _logger.LogInformation("Retrieved {Count} latest holdings for account {AccountId} from date {LatestDate} for real-time pricing", 
                        holdings.Count(), accountId, latestDate.Value);

                    if (_eodMarketDataToolFactory != null)
                    {
                        _logger.LogInformation("Applying real-time pricing for today's date: {Date}", valuationDate);
                        holdings = await ApplyRealTimePricing(holdings.ToList(), cancellationToken);
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
    /// Apply real-time pricing to holdings using EOD market data
    /// </summary>
    private async Task<IEnumerable<Holding>> ApplyRealTimePricing(List<Holding> holdings, CancellationToken cancellationToken)
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

            if (!tickers.Any())
            {
                _logger.LogInformation("No tickers found for real-time pricing");
                return holdings;
            }

            // Fetch real-time prices
            var eodTool = _eodMarketDataToolFactory();
            var prices = await eodTool.GetRealTimePricesAsync(null, tickers, cancellationToken);

            _logger.LogInformation("Fetched {Count} real-time prices for {TotalTickers} tickers", 
                prices.Count, tickers.Count);

            // Log the actual prices fetched for debugging
            foreach (var price in prices)
            {
                _logger.LogInformation("Real-time price: {Ticker} = {Price:C}", price.Key, price.Value);
            }

            // Apply real-time prices to holdings
            var updatedCount = 0;
            foreach (var holding in holdings)
            {
                // Skip CASH holdings - they don't need real-time pricing
                if (holding.Instrument?.Ticker?.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogInformation("Skipping real-time pricing for CASH holding {HoldingId} - keeping original value {CurrentValue:C}", 
                        holding.Id, holding.CurrentValue);
                    continue;
                }

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
                    
                    // Update both current value and valuation date to today for real-time pricing
                    holding.UpdateValuation(DateTime.UtcNow, newCurrentValue);
                    updatedCount++;
                    
                    _logger.LogInformation("Updated holding {HoldingId} for {Ticker}: Original value {OriginalValue:C} -> New value {NewValue:C} (Real-time price: {RealTimePrice}, Scaled price: {ScaledPrice}, Quote unit: {QuoteUnit})", 
                        holding.Id, holding.Instrument.Ticker, originalValue, newCurrentValue, realTimePrice, scaledPrice, holding.Instrument.QuoteUnit ?? CurrencyConstants.DEFAULT_QUOTE_UNIT);
                }
                else
                {
                    _logger.LogWarning("No real-time price found for holding {HoldingId} with ticker {Ticker}", 
                        holding.Id, holding.Instrument?.Ticker ?? "NULL");
                }
            }

            _logger.LogInformation("Successfully updated {UpdatedCount} out of {TotalCount} holdings with real-time pricing", 
                updatedCount, holdings.Count);

            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time pricing, returning holdings with original prices");
            return holdings;
        }
    }
}