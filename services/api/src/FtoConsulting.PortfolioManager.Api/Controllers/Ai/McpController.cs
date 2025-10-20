using Microsoft.AspNetCore.Mvc;
using FtoConsulting.PortfolioManager.Application.Services.Ai;

namespace FtoConsulting.PortfolioManager.Api.Controllers.Ai;

/// <summary>
/// Controller for MCP (Model Context Protocol) server endpoints
/// </summary>
[ApiController]
[Route("api/ai/mcp")]
[Produces("application/json")]
public class McpController : ControllerBase
{
    private readonly IMcpServerService _mcpServerService;
    private readonly ILogger<McpController> _logger;

    public McpController(
        IMcpServerService mcpServerService,
        ILogger<McpController> logger)
    {
        _mcpServerService = mcpServerService;
        _logger = logger;
    }

    /// <summary>
    /// Get available MCP tools
    /// </summary>
    /// <returns>List of available MCP tools with their definitions</returns>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(IEnumerable<McpToolDefinition>), 200)]
    public async Task<IActionResult> GetAvailableTools()
    {
        try
        {
            var tools = await _mcpServerService.GetAvailableToolsAsync();
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MCP tools");
            return StatusCode(500, "An error occurred retrieving MCP tools");
        }
    }

    /// <summary>
    /// Execute an MCP tool
    /// </summary>
    /// <param name="mcpRequest">Tool execution request</param>
    /// <returns>Tool execution result</returns>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExecuteTool([FromBody] McpToolExecutionRequest mcpRequest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mcpRequest.ToolName))
            {
                return BadRequest("Tool name is required");
            }

            _logger.LogInformation("Executing MCP tool: {ToolName}", mcpRequest.ToolName);

            var result = await _mcpServerService.ExecuteToolAsync(
                mcpRequest.ToolName, 
                mcpRequest.Parameters ?? new Dictionary<string, object>());

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid MCP tool request: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MCP tool: {ToolName}", mcpRequest.ToolName);
            return StatusCode(500, "An error occurred executing the tool");
        }
    }

    /// <summary>
    /// Health check for MCP server
    /// </summary>
    /// <returns>MCP server health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var isHealthy = await _mcpServerService.IsHealthyAsync();
            return Ok(new { 
                Status = isHealthy ? "Healthy" : "Unhealthy", 
                Timestamp = DateTime.UtcNow,
                McpServerRunning = isHealthy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MCP server health");
            return Ok(new { 
                Status = "Unhealthy", 
                Timestamp = DateTime.UtcNow,
                Error = "Health check failed"
            });
        }
    }
}

/// <summary>
/// Request model for MCP tool execution
/// </summary>
public class McpToolExecutionRequest
{
    /// <summary>
    /// Name of the tool to execute
    /// </summary>
    public required string ToolName { get; set; }
    
    /// <summary>
    /// Parameters for tool execution
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}