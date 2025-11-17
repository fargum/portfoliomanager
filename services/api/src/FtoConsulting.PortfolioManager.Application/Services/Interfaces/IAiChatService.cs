using OpenAI.Chat;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Interface for AI chat completion to enable easier testing
/// </summary>
public interface IAiChatService
{
    /// <summary>
    /// Complete a chat conversation with AI
    /// </summary>
    /// <param name="messages">Chat messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI response text</returns>
    Task<string> CompleteChatAsync(ChatMessage[] messages, CancellationToken cancellationToken = default);
}