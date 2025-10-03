using FtoConsulting.PortfolioManager.Application.Models;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for fetching market prices using external data providers
/// </summary>
public interface IPriceFetching
{
    /// <summary>
    /// Fetches market prices for all distinct ISINs from holdings on a specific date
    /// This method internally:
    /// 1. Queries distinct ISINs from all portfolio holdings for the date
    /// 2. Uses EOD Historical Data API to fetch current prices for those ISINs
    /// 3. Returns the pricing results with success/failure information
    /// </summary>
    /// <param name="valuationDate">The valuation date to retrieve holdings and fetch prices for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Price fetching results containing successful and failed price retrievals</returns>
    Task<PriceFetchResult> FetchPricesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);
}