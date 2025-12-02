using Microsoft.AspNetCore.Authorization;
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
[Authorize(Policy = "RequirePortfolioScope")]
public class ChatController(
    IAiOrchestrationService aiOrchestrationService,
    ICurrentUserService currentUserService,
    ILogger<ChatController> logger,
    IOptions<AzureFoundryOptions> azureFoundryOptions) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AI.Controller");

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
        
        // SECURITY: Get accountId from authenticated user, NOT from request body
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("thread.id", request.ThreadId?.ToString() ?? "none");
        activity?.SetTag("query.length", request.Query?.Length.ToString() ?? "0");
        activity?.SetTag("response.type", "streaming");
        
        try
        {
            using (logger.BeginScope("AI streaming chat query for account {AccountId}", accountId))
            {
                logger.LogInformation("Processing streaming AI query with AccountId={AccountId}, ThreadId={ThreadId}, QueryLength={QueryLength}",
                    accountId, request.ThreadId, request.Query?.Length ?? 0);
            }
            
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Query cannot be empty");
                activity?.SetTag("error.type", "validation");
                return BadRequest("Query cannot be empty");
            }

            logger.LogInformation("Processing streaming AI chat query with memory for account {AccountId}, thread {ThreadId}: {Query}", 
                accountId, request.ThreadId, request.Query);

            // Set up Server-Sent Events headers
            Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            // Use unified memory-aware streaming with status updates from the AI service
            await aiOrchestrationService.ProcessPortfolioQueryAsync(
                request.Query, 
                accountId,  // SECURITY: Use authenticated accountId, not from request
                onStatusUpdate: async (status) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var statusMessage = new StatusStreamingMessageDto(status);
                        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(statusMessage);
                        await Response.WriteAsync($"{jsonMessage}\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                },
                onTokenReceived: async (token) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var contentMessage = new ContentStreamingMessageDto(token);
                        var jsonMessage = System.Text.Json.JsonSerializer.Serialize(contentMessage);
                        await Response.WriteAsync($"{jsonMessage}\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                },
                request.ThreadId,
                cancellationToken);

            // Send completion message
            if (!cancellationToken.IsCancellationRequested)
            {
                var completionMessage = new CompletionStreamingMessageDto();
                var jsonMessage = System.Text.Json.JsonSerializer.Serialize(completionMessage);
                await Response.WriteAsync($"{jsonMessage}\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            logger.LogInformation("Successfully completed streaming AI chat query with memory for account {AccountId}", 
                accountId);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.completed", "true");

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            logger.LogError(ex, "Error processing streaming AI chat query with memory for account {AccountId}: {Query}", 
                accountId, request.Query);
            
            if (!Response.HasStarted)
            {
                await Response.WriteAsync($"Error: {ex.Message}", cancellationToken);
            }
            return new EmptyResult();
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
            var tools = await aiOrchestrationService.GetAvailableToolsAsync();
            return Ok(tools);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving available AI tools");
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
            AzureFoundryConfigured = !string.IsNullOrEmpty(azureFoundryOptions.Value.Endpoint),
            HasApiKey = !string.IsNullOrEmpty(azureFoundryOptions.Value.ApiKey),
            Endpoint = string.IsNullOrEmpty(azureFoundryOptions.Value.Endpoint) ? "Not configured" : "***CONFIGURED***",
            ApiKey = string.IsNullOrEmpty(azureFoundryOptions.Value.ApiKey) ? "Not configured" : "***CONFIGURED***",
            TimeoutSeconds = azureFoundryOptions.Value.TimeoutSeconds
        });
    }
}
