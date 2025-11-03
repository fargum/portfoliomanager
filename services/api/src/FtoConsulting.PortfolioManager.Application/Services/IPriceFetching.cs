using FtoConsulting.PortfolioManager.Application.Models;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for fetching market prices and exchange rates using external data providers
/// </summary>
public interface IPriceFetching
{
    /// <summary>
    /// Fetches market prices for all distinct ISINs from holdings on a specific date and persists them to the database
    /// This method internally:
    /// 1. Queries distinct ISINs from all portfolio holdings for the date
    /// 2. Uses EOD Historical Data API to fetch current prices for those ISINs
    /// 3. Persists the pricing data to the instrument_prices table
    /// 4. Overwrites any existing data for the same valuation date
    /// </summary>
    /// <param name="valuationDate">The valuation date to retrieve holdings and fetch prices for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Price fetching results containing success/failure information but no actual price data</returns>
    Task<PriceFetchResult> FetchAndPersistPricesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches exchange rates for required currency pairs on a specific date and persists them to the database
    /// This method internally:
    /// 1. Determines required currency pairs from instrument prices (e.g., USD/GBP)
    /// 2. Uses EOD Historical Data API to fetch current FX rates
    /// 3. Persists the exchange rate data to the exchange_rates table
    /// 4. Supports rollforward of previous rates when current data unavailable
    /// </summary>
    /// <param name="valuationDate">The valuation date to fetch exchange rates for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate fetching results containing success/failure information</returns>
    Task<ExchangeRateFetchResult> FetchAndPersistExchangeRatesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);
}