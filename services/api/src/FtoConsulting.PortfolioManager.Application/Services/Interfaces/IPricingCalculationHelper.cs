using FtoConsulting.PortfolioManager.Application.DTOs;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for calculating holding values using current market prices
/// </summary>
public interface IPricingCalculationHelper
{
    /// <summary>
    /// Fetches current price for an instrument and calculates holding value with proper currency conversion
    /// </summary>
    /// <param name="instrumentId">The instrument ID</param>
    /// <param name="ticker">The instrument ticker</param>
    /// <param name="units">Number of units held</param>
    /// <param name="quoteUnit">Quote unit for the instrument (GBP, GBX, USD, etc.)</param>
    /// <param name="priceCurrency">Currency of the price data</param>
    /// <param name="valuationDate">Date to fetch price for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current price and calculated current value with proper currency conversion</returns>
    Task<HoldingPriceResult> FetchAndCalculateHoldingValueAsync(
        int instrumentId,
        string ticker,
        decimal units,
        string? quoteUnit,
        string? priceCurrency,
        DateOnly valuationDate,
        CancellationToken cancellationToken = default);
}