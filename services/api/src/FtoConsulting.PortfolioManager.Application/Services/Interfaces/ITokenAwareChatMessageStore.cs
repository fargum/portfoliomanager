using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Interface for chat message stores that support token-aware message selection
/// </summary>
public interface ITokenAwareChatMessageStore
{
    /// <summary>
    /// Get messages within a specified token budget, prioritizing recent messages
    /// </summary>
    /// <param name="tokenBudget">Maximum number of tokens for the selected messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Messages within the token budget, ordered chronologically</returns>
    Task<IEnumerable<ChatMessage>> GetMessagesWithinTokenBudgetAsync(
        int tokenBudget = 2000,
        CancellationToken cancellationToken = default);
}