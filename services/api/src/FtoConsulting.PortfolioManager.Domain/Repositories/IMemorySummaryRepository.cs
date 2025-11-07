using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for memory summary data access
/// </summary>
public interface IMemorySummaryRepository
{
    Task<MemorySummary?> GetByThreadAndDateAsync(int threadId, DateOnly summaryDate, CancellationToken cancellationToken = default);
    Task<MemorySummary> AddAsync(MemorySummary summary, CancellationToken cancellationToken = default);
}