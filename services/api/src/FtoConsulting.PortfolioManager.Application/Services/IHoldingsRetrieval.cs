using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for retrieving holdings data with related entities
/// </summary>
public interface IHoldingsRetrieval
{
    /// <summary>
    /// Retrieves all holdings for a given account on a specific date
    /// </summary>
    /// <param name="accountId">The account identifier</param>
    /// <param name="valuationDate">The valuation date to filter holdings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of holdings with their related instrument and portfolio data</returns>
    Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(Guid accountId, DateOnly valuationDate, CancellationToken cancellationToken = default);
}