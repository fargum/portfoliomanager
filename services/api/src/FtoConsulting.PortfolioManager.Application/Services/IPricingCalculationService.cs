namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for calculating pricing with proper currency conversion and unit handling
/// </summary>
public interface IPricingCalculationService
{
    /// <summary>
    /// Calculate current value considering quote unit conversion and currency conversion to GBP
    /// </summary>
    /// <param name="unitAmount">Number of shares/units</param>
    /// <param name="price">Price per unit</param>
    /// <param name="quoteUnit">Quote unit (GBP, GBX, USD, etc.) - indicates the unit/scale of the price</param>
    /// <param name="priceCurrency">Currency of the price (USD, GBP, etc.) - indicates the actual currency</param>
    /// <param name="valuationDate">Date for currency conversion rate lookup</param>
    /// <returns>Current value in GBP</returns>
    Task<decimal> CalculateCurrentValueAsync(
        decimal unitAmount, 
        decimal price, 
        string? quoteUnit, 
        string? priceCurrency, 
        DateOnly valuationDate);

    /// <summary>
    /// Infer currency from ticker symbol
    /// </summary>
    /// <param name="ticker">The ticker symbol</param>
    /// <returns>Inferred currency code</returns>
    string GetCurrencyFromTicker(string ticker);

    /// <summary>
    /// Apply scaling factor for proxy instruments that require price adjustments
    /// </summary>
    /// <param name="price">Original price</param>
    /// <param name="ticker">Instrument ticker</param>
    /// <returns>Scaled price</returns>
    decimal ApplyScalingFactor(decimal price, string ticker);
}