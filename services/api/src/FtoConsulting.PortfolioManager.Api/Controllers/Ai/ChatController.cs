using Microsoft.AspNetCore.Mvc;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Api.Controllers.Ai;

/// <summary>
/// Controller for AI-powered chat interface to portfolio data
/// </summary>
[ApiController]
[Route("api/ai/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IAiOrchestrationService _aiOrchestrationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IAiOrchestrationService aiOrchestrationService,
        ILogger<ChatController> logger)
    {
        _aiOrchestrationService = aiOrchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Process a natural language query about portfolio data
    /// </summary>
    /// <param name="request">Chat request containing query and account information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated response based on portfolio data</returns>
    [HttpPost("query")]
    [ProducesResponseType(typeof(ChatResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> QueryPortfolio(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Query cannot be empty");
            }

            if (request.AccountId <= 0)
            {
                return BadRequest("Valid account ID is required");
            }

            _logger.LogInformation("Processing AI chat query for account {AccountId}: {Query}", 
                request.AccountId, request.Query);

            var response = await _aiOrchestrationService.ProcessPortfolioQueryAsync(
                request.Query, 
                request.AccountId,
                cancellationToken);

            _logger.LogInformation("Successfully processed AI chat query for account {AccountId}", 
                request.AccountId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI chat query for account {AccountId}: {Query}", 
                request.AccountId, request.Query);
            return StatusCode(500, "An error occurred processing your request");
        }
    }

    /// <summary>
    /// Get available AI tools and capabilities
    /// </summary>
    /// <returns>List of available AI tools</returns>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(IEnumerable<AiToolDto>), 200)]
    public async Task<IActionResult> GetAvailableTools()
    {
        try
        {
            var tools = await _aiOrchestrationService.GetAvailableToolsAsync();
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available AI tools");
            return StatusCode(500, "An error occurred retrieving available tools");
        }
    }

    /// <summary>
    /// Health check for AI chat services
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public IActionResult GetHealth()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}