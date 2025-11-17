using FtoConsulting.PortfolioManager.Application.Models;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for revaluing holdings based on current market prices
/// </summary>
public interface IHoldingRevaluationService
{
    /// <summary>
    /// Revalues holdings for a specific valuation date using prices from the instrument_prices table
    /// </summary>
    /// <param name="valuationDate">The date to revalue holdings for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing revaluation statistics</returns>
    Task<HoldingRevaluationResult> RevalueHoldingsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches market prices and then revalues holdings for a specific valuation date
    /// This is a combined operation that first fetches current market prices and then applies them to revalue holdings
    /// </summary>
    /// <param name="valuationDate">The date to fetch prices and revalue holdings for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined result containing both price fetch and revaluation statistics</returns>
    Task<CombinedPriceAndRevaluationResult> FetchPricesAndRevalueHoldingsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);
}