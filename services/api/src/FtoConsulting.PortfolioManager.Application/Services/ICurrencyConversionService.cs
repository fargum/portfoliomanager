namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for handling currency conversions
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts an amount from one currency to another using the latest available rate
    /// </summary>
    /// <param name="amount">Amount to convert</param>
    /// <param name="fromCurrency">Source currency (e.g., "USD")</param>
    /// <param name="toCurrency">Target currency (e.g., "GBP")</param>
    /// <param name="valuationDate">Date for the conversion rate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Converted amount and the exchange rate used</returns>
    Task<(decimal convertedAmount, decimal exchangeRate, string rateSource)> ConvertCurrencyAsync(
        decimal amount, 
        string fromCurrency, 
        string toCurrency, 
        DateOnly valuationDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the exchange rate between two currencies for a specific date
    /// </summary>
    /// <param name="fromCurrency">Source currency</param>
    /// <param name="toCurrency">Target currency</param>
    /// <param name="valuationDate">Date for the rate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate if available, null otherwise</returns>
    Task<decimal?> GetExchangeRateAsync(
        string fromCurrency, 
        string toCurrency, 
        DateOnly valuationDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a currency conversion is supported
    /// </summary>
    /// <param name="fromCurrency">Source currency</param>
    /// <param name="toCurrency">Target currency</param>
    /// <returns>True if conversion is supported, false otherwise</returns>
    bool IsConversionSupported(string fromCurrency, string toCurrency);
}