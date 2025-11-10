using FtoConsulting.PortfolioManager.Application.DTOs;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for real-time portfolio valuation using live market data
/// This service fetches the latest holdings and applies current market prices
/// without persisting the real-time data to the database
/// </summary>
public interface IRealTimePortfolioService
{
    /// <summary>
    /// Get real-time portfolio valuation for a specific account
    /// Uses the latest available holdings data and applies current market prices
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Real-time portfolio data with current market values</returns>
    Task<RealTimePortfolioDto> GetRealTimePortfolioAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get real-time holding values for a specific account
    /// Returns individual holding details with current market prices
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of holdings with real-time market values</returns>
    Task<IEnumerable<RealTimeHoldingDto>> GetRealTimeHoldingsAsync(int accountId, CancellationToken cancellationToken = default);
}