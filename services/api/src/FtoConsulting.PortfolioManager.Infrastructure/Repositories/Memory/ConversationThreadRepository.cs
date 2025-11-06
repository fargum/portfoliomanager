using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories.Memory;

/// <summary>
/// Repository implementation for conversation thread data access
/// </summary>
public class ConversationThreadRepository : IConversationThreadRepository
{
    private readonly PortfolioManagerDbContext _dbContext;

    public ConversationThreadRepository(PortfolioManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Get the most recent active thread for an account
    /// </summary>
    public async Task<ConversationThread?> GetMostRecentActiveThreadAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConversationThreads
            .Where(ct => ct.AccountId == accountId && ct.IsActive)
            .OrderByDescending(ct => ct.LastActivity)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get a thread by ID with related details
    /// </summary>
    public async Task<ConversationThread?> GetByIdWithDetailsAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConversationThreads
            .Include(ct => ct.Messages.OrderBy(m => m.MessageTimestamp).Take(10))
            .Include(ct => ct.Summaries.OrderByDescending(s => s.SummaryDate).Take(5))
            .FirstOrDefaultAsync(ct => ct.Id == threadId && ct.AccountId == accountId, cancellationToken);
    }

    /// <summary>
    /// Get active threads for an account
    /// </summary>
    public async Task<IEnumerable<ConversationThread>> GetActiveThreadsAsync(int accountId, int maxCount = 20, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConversationThreads
            .Where(ct => ct.AccountId == accountId && ct.IsActive)
            .OrderByDescending(ct => ct.LastActivity)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Add a new thread
    /// </summary>
    public async Task<ConversationThread> AddAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        _dbContext.ConversationThreads.Add(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return thread;
    }

    /// <summary>
    /// Update an existing thread
    /// </summary>
    public async Task<ConversationThread> UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        _dbContext.ConversationThreads.Update(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return thread;
    }

    /// <summary>
    /// Get inactive old threads for cleanup
    /// </summary>
    public async Task<IEnumerable<ConversationThread>> GetInactiveOldThreadsAsync(int accountId, DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConversationThreads
            .Where(ct => ct.AccountId == accountId && 
                       !ct.IsActive && 
                       ct.LastActivity < cutoffDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Delete multiple threads
    /// </summary>
    public async Task DeleteRangeAsync(IEnumerable<ConversationThread> threads, CancellationToken cancellationToken = default)
    {
        _dbContext.ConversationThreads.RemoveRange(threads);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}