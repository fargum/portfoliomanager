using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for orchestrating AI-powered portfolio queries and analysis
/// </summary>
public interface IAiOrchestrationService
{
    /// <summary>
    /// Process a natural language query about portfolio data with memory support
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="accountId">The account ID to query</param>
    /// <param name="threadId">Optional conversation thread ID for memory continuity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated response based on portfolio data with memory context</returns>
    Task<ChatResponseDto> ProcessPortfolioQueryWithMemoryAsync(string query, int accountId, int? threadId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process a natural language query about portfolio data with streaming response and memory support
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="accountId">The account ID to query</param>
    /// <param name="onTokenReceived">Callback for each streaming token received</param>
    /// <param name="threadId">Optional conversation thread ID for memory continuity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when streaming is finished</returns>
    Task ProcessPortfolioQueryStreamWithMemoryAsync(string query, int accountId, Func<string, Task> onTokenReceived, int? threadId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available AI tools for the MCP server
    /// </summary>
    /// <returns>List of available tools</returns>
    Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync();
}