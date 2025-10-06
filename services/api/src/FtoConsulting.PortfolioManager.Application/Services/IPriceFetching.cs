using FtoConsulting.PortfolioManager.Application.Models;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for fetching market prices using external data providers
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
}