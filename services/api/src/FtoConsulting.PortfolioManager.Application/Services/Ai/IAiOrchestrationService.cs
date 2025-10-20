using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Service for orchestrating AI-powered portfolio queries and analysis
/// </summary>
public interface IAiOrchestrationService
{
    /// <summary>
    /// Process a natural language query about portfolio data
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="accountId">The account ID to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated response based on portfolio data</returns>
    Task<ChatResponseDto> ProcessPortfolioQueryAsync(string query, int accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available AI tools for the MCP server
    /// </summary>
    /// <returns>List of available tools</returns>
    Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync();
}