using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories.Memory;

/// <summary>
/// Repository implementation for chat message data access
/// </summary>
public class ChatMessageRepository : IChatMessageRepository
{
    private readonly PortfolioManagerDbContext _dbContext;

    public ChatMessageRepository(PortfolioManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Get messages within a date range for a specific thread
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetMessagesByDateRangeAsync(int threadId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .Where(cm => cm.ConversationThreadId == threadId && 
                       cm.MessageTimestamp >= startDate && 
                       cm.MessageTimestamp <= endDate)
            .OrderBy(cm => cm.MessageTimestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get all messages for a specific thread
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetByThreadIdAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatMessages
            .Where(cm => cm.ConversationThreadId == threadId)
            .OrderBy(cm => cm.MessageTimestamp)
            .ToListAsync(cancellationToken);
    }
}