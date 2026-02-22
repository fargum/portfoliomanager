using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Comprehensive service for all holding operations (CRUD and retrieval)
/// </summary>
public interface IHoldingService
{
    // Read Operations
    
    /// <summary>
    /// Retrieves all holdings for a given account on a specific date
    /// </summary>
    /// <param name="accountId">The account identifier</param>
    /// <param name="valuationDate">The valuation date to filter holdings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of holdings with their related instrument and portfolio data</returns>
    Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves holdings for a given account, date, and ticker symbol
    /// </summary>
    Task<IEnumerable<Holding>> GetHoldingsByAccountDateAndTickerAsync(int accountId, DateOnly valuationDate, string ticker, CancellationToken cancellationToken = default);

    // Create Operations
    
    /// <summary>
    /// Adds a new holding to a portfolio for the latest valuation date
    /// </summary>
    /// <param name="portfolioId">Portfolio to add holding to</param>
    /// <param name="request">Details of the holding to add</param>
    /// <param name="accountId">Account ID for authorization check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the add operation</returns>
    Task<HoldingAddResult> AddHoldingAsync(
        int portfolioId,
        AddHoldingRequest request,
        int accountId,
        CancellationToken cancellationToken = default);

    // Update Operations
    
    /// <summary>
    /// Updates the unit amount for an existing holding on the latest valuation date
    /// </summary>
    /// <param name="holdingId">ID of the holding to update</param>
    /// <param name="newUnits">New unit amount</param>
    /// <param name="accountId">Account ID for authorization check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the update operation</returns>
    Task<HoldingUpdateResult> UpdateHoldingUnitsAsync(
        int holdingId,
        decimal newUnits,
        int accountId,
        CancellationToken cancellationToken = default);

    // Delete Operations
    
    /// <summary>
    /// Removes a holding from the latest valuation date
    /// </summary>
    /// <param name="holdingId">ID of the holding to remove</param>
    /// <param name="accountId">Account ID for authorization check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the delete operation</returns>
    Task<HoldingDeleteResult> DeleteHoldingAsync(
        int holdingId,
        int accountId,
        CancellationToken cancellationToken = default);
}