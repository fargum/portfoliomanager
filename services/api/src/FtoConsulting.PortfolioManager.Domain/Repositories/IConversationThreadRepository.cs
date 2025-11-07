using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for conversation thread data access
/// </summary>
public interface IConversationThreadRepository
{
    Task<ConversationThread?> GetMostRecentActiveThreadAsync(int accountId, CancellationToken cancellationToken = default);
    Task<ConversationThread?> GetByIdWithDetailsAsync(int threadId, int accountId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationThread>> GetActiveThreadsAsync(int accountId, int maxCount = 20, CancellationToken cancellationToken = default);
    Task<ConversationThread> AddAsync(ConversationThread thread, CancellationToken cancellationToken = default);
    Task<ConversationThread> UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationThread>> GetInactiveOldThreadsAsync(int accountId, DateTime cutoffDate, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<ConversationThread> threads, CancellationToken cancellationToken = default);
}