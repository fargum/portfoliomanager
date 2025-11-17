using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Constants;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for handling currency conversions
/// </summary>
public class CurrencyConversionService : ICurrencyConversionService
{
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly ILogger<CurrencyConversionService> _logger;

    // Supported currency pairs for conversion
    private static readonly string[] SupportedCurrencies = { CurrencyConstants.GBP, CurrencyConstants.USD, CurrencyConstants.EUR, CurrencyConstants.GBX };
    private const string BaseCurrency = CurrencyConstants.DEFAULT_BASE_CURRENCY; // Portfolio base currency

    public CurrencyConversionService(
        IExchangeRateRepository exchangeRateRepository,
        ILogger<CurrencyConversionService> logger)
    {
        _exchangeRateRepository = exchangeRateRepository ?? throw new ArgumentNullException(nameof(exchangeRateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(decimal convertedAmount, decimal exchangeRate, string rateSource)> ConvertCurrencyAsync(
        decimal amount, 
        string fromCurrency, 
        string toCurrency, 
        DateOnly valuationDate, 
        CancellationToken cancellationToken = default)
    {
        fromCurrency = fromCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(fromCurrency));
        toCurrency = toCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(toCurrency));

        // Handle special cases
        if (fromCurrency == toCurrency)
        {
            return (amount, 1.0m, "SAME_CURRENCY");
        }

        // Handle GBX to GBP conversion (quote unit conversion, not FX)
        if (fromCurrency == CurrencyConstants.GBX && toCurrency == CurrencyConstants.GBP)
        {
            return (amount / 100m, 0.01m, "QUOTE_UNIT_CONVERSION");
        }

        if (fromCurrency == CurrencyConstants.GBP && toCurrency == CurrencyConstants.GBX)
        {
            return (amount * 100m, 100m, "QUOTE_UNIT_CONVERSION");
        }

        // Get exchange rate from database
        var exchangeRate = await _exchangeRateRepository.GetLatestRateAsync(fromCurrency, toCurrency, valuationDate, cancellationToken);
        
        if (exchangeRate != null)
        {
            var convertedAmount = amount * exchangeRate.Rate;
            _logger.LogDebug("Converted {Amount} {FromCurrency} to {ConvertedAmount} {ToCurrency} using rate {Rate} from {Source}", 
                amount, fromCurrency, convertedAmount, toCurrency, exchangeRate.Rate, exchangeRate.Source);
            
            
return (convertedAmount, exchangeRate.Rate, exchangeRate.Source);
        }

        // Try inverse rate (e.g., if we need USD/GBP but only have GBP/USD)
        var inverseRate = await _exchangeRateRepository.GetLatestRateAsync(toCurrency, fromCurrency, valuationDate, cancellationToken);
        
        if (inverseRate != null && inverseRate.Rate != 0)
        {
            var rate = 1m / inverseRate.Rate;
            var convertedAmount = amount * rate;
            _logger.LogDebug("Converted {Amount} {FromCurrency} to {ConvertedAmount} {ToCurrency} using inverse rate {Rate} from {Source}", 
                amount, fromCurrency, convertedAmount, toCurrency, rate, inverseRate.Source);
            
            
return (convertedAmount, rate, $"INVERSE_{inverseRate.Source}");
        }

        // No exchange rate available
        throw new InvalidOperationException($"No exchange rate available for {fromCurrency}/{toCurrency} on or before {valuationDate:yyyy-MM-dd}");
    }

    public async Task<decimal?> GetExchangeRateAsync(
        string fromCurrency, 
        string toCurrency, 
        DateOnly valuationDate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (_, exchangeRate, _) = await ConvertCurrencyAsync(1m, fromCurrency, toCurrency, valuationDate, cancellationToken);
            return exchangeRate;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public bool IsConversionSupported(string fromCurrency, string toCurrency)
    {
        fromCurrency = fromCurrency?.ToUpperInvariant() ?? string.Empty;
        toCurrency = toCurrency?.ToUpperInvariant() ?? string.Empty;

        return SupportedCurrencies.Contains(fromCurrency) && SupportedCurrencies.Contains(toCurrency);
    }
}
