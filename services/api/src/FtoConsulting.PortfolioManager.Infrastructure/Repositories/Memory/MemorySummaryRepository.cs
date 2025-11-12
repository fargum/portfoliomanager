using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories.Memory;

/// <summary>
/// Repository implementation for memory summary data access
/// </summary>
public class MemorySummaryRepository : IMemorySummaryRepository
{
    private readonly PortfolioManagerDbContext _dbContext;

    public MemorySummaryRepository(PortfolioManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Get a summary by thread and date
    /// </summary>
    public async Task<MemorySummary?> GetByThreadAndDateAsync(int threadId, DateOnly summaryDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MemorySummaries
            .FirstOrDefaultAsync(ms => ms.ConversationThreadId == threadId && ms.SummaryDate == summaryDate, cancellationToken);
    }

    /// <summary>
    /// Add a new memory summary
    /// </summary>
    public async Task<MemorySummary> AddAsync(MemorySummary summary, CancellationToken cancellationToken = default)
    {
        _dbContext.MemorySummaries.Add(summary);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return summary;
    }

    /// <summary>
    /// Get recent memory summaries for an account (across all threads)
    /// </summary>
    public async Task<IEnumerable<MemorySummary>> GetRecentSummariesByAccountAsync(int accountId, int limit, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == accountId)
            .OrderByDescending(ms => ms.SummaryDate)
            .ThenByDescending(ms => ms.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}