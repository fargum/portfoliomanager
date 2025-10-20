using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Service for fetching market intelligence and external market data
/// </summary>
public interface IMarketIntelligenceService
{
    /// <summary>
    /// Get market context for specific tickers on a given date
    /// </summary>
    /// <param name="tickers">Stock tickers to analyze</param>
    /// <param name="date">Date for market analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market context and news for the specified tickers</returns>
    Task<MarketContextDto> GetMarketContextAsync(IEnumerable<string> tickers, DateTime date, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for financial news related to specific instruments
    /// </summary>
    /// <param name="tickers">Stock tickers to search news for</param>
    /// <param name="fromDate">Start date for news search</param>
    /// <param name="toDate">End date for news search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Relevant financial news</returns>
    Task<IEnumerable<NewsItemDto>> SearchFinancialNewsAsync(IEnumerable<string> tickers, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get general market sentiment and indices performance
    /// </summary>
    /// <param name="date">Date for market analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market sentiment data</returns>
    Task<MarketSentimentDto> GetMarketSentimentAsync(DateTime date, CancellationToken cancellationToken = default);
}