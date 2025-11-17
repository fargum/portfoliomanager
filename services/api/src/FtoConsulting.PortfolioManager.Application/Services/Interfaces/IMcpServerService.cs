namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service interface for MCP (Model Context Protocol) server functionality
/// </summary>
public interface IMcpServerService
{
    /// <summary>
    /// Initialize the MCP server with available tools
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process an MCP tool call request
    /// </summary>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="parameters">Tool parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available MCP tools
    /// </summary>
    /// <returns>List of available tools with their definitions</returns>
    Task<IEnumerable<McpToolDefinition>> GetAvailableToolsAsync();
    
    /// <summary>
    /// Check if the MCP server is healthy
    /// </summary>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync();
    
    /// <summary>
    /// Call a tool on an external MCP server
    /// </summary>
    /// <param name="serverId">External MCP server identifier</param>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="parameters">Tool parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<object> CallMcpToolAsync(string serverId, string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP tool definition
/// </summary>
public record McpToolDefinition(
    string Name,
    string Description,
    Dictionary<string, object> Schema
);