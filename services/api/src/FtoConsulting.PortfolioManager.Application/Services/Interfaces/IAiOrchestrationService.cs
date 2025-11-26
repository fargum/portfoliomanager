using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for orchestrating AI-powered portfolio queries and analysis
/// </summary>
public interface IAiOrchestrationService
{
    /// <summary>
    /// Process a natural language query about portfolio data with streaming support, memory, and status updates
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="accountId">The account ID to query</param>
    /// <param name="onStatusUpdate">Optional callback for status updates during processing</param>
    /// <param name="onTokenReceived">Callback for streaming tokens</param>
    /// <param name="threadId">Optional conversation thread ID for memory continuity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when streaming finishes</returns>
    Task ProcessPortfolioQueryAsync(
        string query, 
        int accountId, 
        Func<StatusUpdateDto, Task>? onStatusUpdate,
        Func<string, Task> onTokenReceived,
        int? threadId = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available AI tools for the MCP server
    /// </summary>
    /// <returns>List of available tools</returns>
    Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync();
}