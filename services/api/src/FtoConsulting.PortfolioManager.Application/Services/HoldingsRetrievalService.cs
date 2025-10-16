using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for retrieving holdings data with related entities
/// </summary>
public class HoldingsRetrievalService : IHoldingsRetrieval
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly ILogger<HoldingsRetrievalService> _logger;

    public HoldingsRetrievalService(
        IHoldingRepository holdingRepository,
        ILogger<HoldingsRetrievalService> logger)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);

        try
        {
            var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
            
            _logger.LogInformation("Retrieved {Count} holdings for account {AccountId} on date {ValuationDate}", 
                holdings.Count(), accountId, valuationDate);
            
            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            throw;
        }
    }
}