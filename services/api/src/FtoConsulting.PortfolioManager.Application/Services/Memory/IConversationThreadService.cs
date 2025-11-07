using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services.Memory;

/// <summary>
/// Service for managing conversation threads and memory operations
/// </summary>
public interface IConversationThreadService
{
    Task<ConversationThread> GetOrCreateActiveThreadAsync(int accountId, CancellationToken cancellationToken = default);
    Task<ConversationThread?> GetThreadByIdAsync(int threadId, int accountId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationThread>> GetActiveThreadsForAccountAsync(int accountId, CancellationToken cancellationToken = default);
    Task<ConversationThread> CreateNewThreadAsync(int accountId, string title, CancellationToken cancellationToken = default);
    Task<ConversationThread> UpdateThreadTitleAsync(int threadId, int accountId, string title, CancellationToken cancellationToken = default);
    Task DeactivateThreadAsync(int threadId, int accountId, CancellationToken cancellationToken = default);
    Task<MemorySummary> CreateDailySummaryAsync(int threadId, DateOnly summaryDate, CancellationToken cancellationToken = default);
    Task CleanupOldThreadsAsync(int accountId, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}