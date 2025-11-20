using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using System.Diagnostics;


namespace FtoConsulting.PortfolioManager.Api.Controllers.Ai;

/// <summary>
/// Controller for AI-powered chat interface to portfolio data
/// </summary>
[ApiController]
[Route("api/ai/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AI.Controller");
    
    private readonly IAiOrchestrationService _aiOrchestrationService;
    private readonly ILogger<ChatController> _logger;
    private readonly AzureFoundryOptions _azureFoundryOptions;

    public ChatController(
        IAiOrchestrationService aiOrchestrationService,
        ILogger<ChatController> logger,
        IOptions<AzureFoundryOptions> azureFoundryOptions)
    {
        _aiOrchestrationService = aiOrchestrationService;
        _logger = logger;
        _azureFoundryOptions = azureFoundryOptions.Value;
    }

    /// <summary>
    /// Process a natural language query about portfolio data with streaming response and memory
    /// </summary>
    /// <param name="request">Chat request containing query and account information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming AI-generated response based on portfolio data with conversation memory</returns>
    [HttpPost("stream")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> StreamPortfolioQuery(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("StreamPortfolioQuery");
        activity?.SetTag("account.id", request.AccountId.ToString());
        activity?.SetTag("thread.id", request.ThreadId?.ToString() ?? "none");
        activity?.SetTag("query.length", request.Query?.Length.ToString() ?? "0");
        activity?.SetTag("response.type", "streaming");
        
        try
        {
            using (_logger.BeginScope("AI streaming chat query for account {AccountId}", request.AccountId))
            {
                _logger.LogInformation("Processing streaming AI query with AccountId={AccountId}, ThreadId={ThreadId}, QueryLength={QueryLength}",
                    request.AccountId, request.ThreadId, request.Query?.Length ?? 0);
            }
            
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Query cannot be empty");
                activity?.SetTag("error.type", "validation");
                return BadRequest("Query cannot be empty");
            }

            if (request.AccountId <= 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Valid account ID is required");
                activity?.SetTag("error.type", "validation");
                return BadRequest("Valid account ID is required");
            }

            _logger.LogInformation("Processing streaming AI chat query with memory for account {AccountId}, thread {ThreadId}: {Query}", 
                request.AccountId, request.ThreadId, request.Query);

            // Set up Server-Sent Events headers
            Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            // Use memory-aware streaming from the AI service
            await _aiOrchestrationService.ProcessPortfolioQueryStreamWithMemoryAsync(
                request.Query, 
                request.AccountId,
                async (token) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Response.WriteAsync(token, cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                },
                request.ThreadId,
                cancellationToken);

            _logger.LogInformation("Successfully completed streaming AI chat query with memory for account {AccountId}", 
                request.AccountId);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.completed", "true");

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            _logger.LogError(ex, "Error processing streaming AI chat query with memory for account {AccountId}: {Query}", 
                request.AccountId, request.Query);
            
            if (!Response.HasStarted)
            {
                await Response.WriteAsync($"Error: {ex.Message}", cancellationToken);
            }
            return new EmptyResult();
        }
    }

    /// <summary>
    /// Process a natural language query about portfolio data with memory support
    /// </summary>
    /// <param name="request">Chat request containing query and account information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated response based on portfolio data with conversation memory</returns>
    [HttpPost("query")]
    [ProducesResponseType(typeof(ChatResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> QueryPortfolio(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("QueryPortfolio");
        activity?.SetTag("account.id", request.AccountId.ToString());
        activity?.SetTag("thread.id", request.ThreadId?.ToString() ?? "none");
        activity?.SetTag("query.length", request.Query?.Length.ToString() ?? "0");
        activity?.SetTag("response.type", "direct");
        
        try
        {
            using (_logger.BeginScope("AI portfolio query for account {AccountId}", request.AccountId))
            {
                _logger.LogInformation("Processing AI portfolio query with AccountId={AccountId}, ThreadId={ThreadId}, QueryLength={QueryLength}",
                    request.AccountId, request.ThreadId, request.Query?.Length ?? 0);
            }
            
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Query cannot be empty");
                activity?.SetTag("error.type", "validation");
                return BadRequest("Query cannot be empty");
            }

            if (request.AccountId <= 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Valid account ID is required");
                activity?.SetTag("error.type", "validation");
                return BadRequest("Valid account ID is required");
            }

            _logger.LogInformation("Processing AI chat query with memory for account {AccountId}, thread {ThreadId}: {Query}", 
                request.AccountId, request.ThreadId, request.Query);

            var response = await _aiOrchestrationService.ProcessPortfolioQueryWithMemoryAsync(
                request.Query, 
                request.AccountId,
                request.ThreadId,
                cancellationToken);

            _logger.LogInformation("Successfully processed AI chat query with memory for account {AccountId}", 
                request.AccountId);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.completed", "true");

            return Ok(response);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            _logger.LogError(ex, "Error processing AI chat query with memory for account {AccountId}: {Query}", 
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

    /// <summary>
    /// Test endpoint to verify Azure Foundry configuration is loaded
    /// </summary>
    /// <returns>Configuration status (sensitive data masked)</returns>
    [HttpGet("config-test")]
    [ProducesResponseType(200)]
    public IActionResult GetConfigTest()
    {
        return Ok(new 
        { 
            AzureFoundryConfigured = !string.IsNullOrEmpty(_azureFoundryOptions.Endpoint),
            HasApiKey = !string.IsNullOrEmpty(_azureFoundryOptions.ApiKey),
            Endpoint = string.IsNullOrEmpty(_azureFoundryOptions.Endpoint) ? "Not configured" : "***CONFIGURED***",
            ApiKey = string.IsNullOrEmpty(_azureFoundryOptions.ApiKey) ? "Not configured" : "***CONFIGURED***",
            TimeoutSeconds = _azureFoundryOptions.TimeoutSeconds
        });
    }
}
