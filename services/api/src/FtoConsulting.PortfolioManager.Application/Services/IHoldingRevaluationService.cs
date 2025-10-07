using FtoConsulting.PortfolioManager.Application.Models;

namespace FtoConsulting.PortfolioManager.Application.Services;

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
}