using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for chat message data access
/// </summary>
public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetMessagesByDateRangeAsync(int threadId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}