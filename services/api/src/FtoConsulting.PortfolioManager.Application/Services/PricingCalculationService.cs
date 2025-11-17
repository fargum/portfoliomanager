using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Domain.Constants;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for calculating pricing with proper currency conversion and unit handling
/// </summary>
public class PricingCalculationService : IPricingCalculationService
{
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly ILogger<PricingCalculationService> _logger;

    public PricingCalculationService(
        ICurrencyConversionService currencyConversionService,
        ILogger<PricingCalculationService> logger)
    {
        _currencyConversionService = currencyConversionService ?? throw new ArgumentNullException(nameof(currencyConversionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculate current value considering quote unit conversion and currency conversion to GBP
    /// </summary>
    public async Task<decimal> CalculateCurrentValueAsync(
        decimal unitAmount, 
        decimal price, 
        string? quoteUnit, 
        string? priceCurrency, 
        DateOnly valuationDate)
    {
        // Default to GBP if no quote unit specified
        var effectiveQuoteUnit = quoteUnit?.ToUpperInvariant() ?? CurrencyConstants.DEFAULT_QUOTE_UNIT;
        var effectivePriceCurrency = priceCurrency?.ToUpperInvariant() ?? CurrencyConstants.DEFAULT_BASE_CURRENCY;
        
        // Step 1: Convert price based on quote unit (scale adjustment)
        // This handles pence vs pounds, not currency conversion
        decimal adjustedPrice = effectiveQuoteUnit switch
        {
            CurrencyConstants.GBX => price / 100m, // Convert pence to pounds (100 pence = 1 pound)
            CurrencyConstants.GBP => price,        // Already in pounds
            CurrencyConstants.USD => price,        // USD price as-is (currency conversion happens later)
            CurrencyConstants.EUR => price,        // EUR price as-is (currency conversion happens later)
            _ => price             // Default to no scale conversion for unknown units
        };

        // Step 2: Calculate gross value in the original currency
        var grossValue = unitAmount * adjustedPrice;

        // Step 3: Handle currency conversion if needed
        // For UK securities: GBX and GBP both represent GBP currency (just different units)
        // For foreign securities: USD quoteUnit = USD currency, EUR quoteUnit = EUR currency
        var actualCurrency = effectiveQuoteUnit switch
        {
            CurrencyConstants.GBX => CurrencyConstants.GBP, // Pence are still GBP currency
            CurrencyConstants.GBP => CurrencyConstants.GBP, // Already GBP currency
            CurrencyConstants.USD => CurrencyConstants.USD, // USD currency
            CurrencyConstants.EUR => CurrencyConstants.EUR, // EUR currency
            _ => effectivePriceCurrency ?? CurrencyConstants.DEFAULT_BASE_CURRENCY // Default to price currency or GBP
        };
        
        if (actualCurrency != CurrencyConstants.GBP)
        {
            try
            {
                var conversionResult = await _currencyConversionService.ConvertCurrencyAsync(
                    grossValue, 
                    actualCurrency, 
                    CurrencyConstants.GBP, 
                    valuationDate);

                return conversionResult.convertedAmount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert {GrossValue} {Currency} to GBP for {ValuationDate}, using unconverted value", 
                    grossValue, actualCurrency, valuationDate);
                
                
// Fallback: return unconverted value (may not be accurate but allows processing to continue)
                return grossValue;
            }
        }

        // Already in GBP (including GBX which is pence but GBP currency), return as-is
        return grossValue;
    }

    /// <summary>
    /// Infer currency from ticker symbol
    /// </summary>
    public string GetCurrencyFromTicker(string ticker)
    {
        // Simple heuristic based on exchange suffix
        return ticker.ToUpperInvariant() switch
        {
            var t when t.EndsWith(ExchangeConstants.LSE_SUFFIX) || t.EndsWith(ExchangeConstants.LONDON_SUFFIX) => CurrencyConstants.GBP,
            var t when t.EndsWith(ExchangeConstants.US_SUFFIX) => CurrencyConstants.USD, 
            var t when t.EndsWith(ExchangeConstants.PARIS_SUFFIX) => CurrencyConstants.EUR,
            var t when t.EndsWith(ExchangeConstants.DEUTSCHE_SUFFIX) => CurrencyConstants.EUR,
            _ => CurrencyConstants.DEFAULT_BASE_CURRENCY // Default to GBP
        };
    }

    /// <summary>
    /// Apply scaling factor for proxy instruments that require price adjustments
    /// </summary>
    public decimal ApplyScalingFactor(decimal price, string ticker)
    {
        if (ticker.Equals(ExchangeConstants.ISF_TICKER, StringComparison.OrdinalIgnoreCase))
        {
            var scaledPrice = price * ExchangeConstants.ISF_SCALING_FACTOR;
            _logger.LogInformation("Applied scaling factor {ScalingFactor} to {Ticker}: Original price={OriginalPrice}, Scaled price={ScaledPrice}", 
                ExchangeConstants.ISF_SCALING_FACTOR, ticker, price, scaledPrice);
            return scaledPrice;
        }
        
        // No scaling needed for this ticker
        return price;
    }
}
