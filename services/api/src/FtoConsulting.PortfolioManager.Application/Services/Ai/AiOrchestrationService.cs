using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Guardrails;
using FtoConsulting.PortfolioManager.Domain.Entities;
using System.Diagnostics;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of AI orchestration service for portfolio queries with enhanced guardrails
/// </summary>
public class AiOrchestrationService(
    ILogger<AiOrchestrationService> logger,
    IOptions<AzureFoundryOptions> azureFoundryOptions,
    IMcpServerService mcpServerService,
    IConversationThreadService conversationThreadService,
    IAgentPromptService agentPromptService,
    AgentFrameworkGuardrails guardrails,
    Func<int, int?, System.Text.Json.JsonSerializerOptions?, ChatHistoryProvider> chatMessageStoreFactory,
    Func<int, IChatClient, AIContextProvider> memoryContextProviderFactory,
    ILoggerFactory loggerFactory) : IAiOrchestrationService
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AI");

    // Lazy-loaded OpenAI client pointing to the Foundry /openai/v1/ endpoint — works for all deployed models
    private readonly Lazy<OpenAIClient> _openAiClient = new Lazy<OpenAIClient>(() =>
        new OpenAIClient(
            new ApiKeyCredential(azureFoundryOptions.Value.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(azureFoundryOptions.Value.FoundryProjectEndpoint) }));

    public async Task ProcessPortfolioQueryAsync(
        string query, 
        int accountId, 
        Func<StatusUpdateDto, Task>? onStatusUpdate,
        Func<string, Task> onTokenReceived,
        int? threadId = null,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: Validate threadId belongs to authenticated account if provided
        ConversationThread? thread = null;
        if (threadId.HasValue)
        {
            thread = await conversationThreadService.GetThreadByIdAsync(threadId.Value, accountId, cancellationToken);
            if (thread == null)
            {
                logger.LogWarning("ThreadId {ThreadId} not found or does not belong to account {AccountId}, creating new thread", 
                    threadId.Value, accountId);
            }
        }
        
        // Get or create conversation thread if not already retrieved
        thread ??= await conversationThreadService.GetOrCreateActiveThreadAsync(accountId, cancellationToken);
        
        // GUARDRAILS: Validate input
        var inputValidation = await guardrails.ValidateInputAsync(query, accountId);
        
        if (!inputValidation.IsValid)
        {
            await guardrails.LogSecurityIncident(inputValidation, accountId, "ProcessPortfolioQueryAsync");
            
            var fallbackResponse = guardrails.CreateFallbackResponse(inputValidation, accountId);
            await onTokenReceived(fallbackResponse);
            return;
        }
        
        try
        {
            await ProcessWithStreamingComponents(
                query, 
                accountId, 
                thread.Id,
                modelId,
                onStatusUpdate, 
                onTokenReceived, 
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing portfolio query for account {AccountId}, thread {ThreadId}", accountId, thread.Id);
            
            var errorResponse = "I apologize, but I encountered an error while processing your request. Please try again.";
            await onTokenReceived(errorResponse);
        }
    }

    public async Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync()
    {
        await Task.CompletedTask;
        return PortfolioToolRegistry.GetAiToolDtos();
    }

    /// <summary>
    /// Create AI functions that connect to our MCP server tools
    /// SECURITY: accountId is injected from authenticated context, not exposed to AI
    /// </summary>
    private IEnumerable<AITool> CreatePortfolioMcpFunctions(int authenticatedAccountId)
    {
        // Create AI functions from the centralized tool registry
        // These will be used by the AI agent to call our MCP tools
        // Pass the authenticated account ID to prevent cross-account data access
        return PortfolioToolRegistry.CreateAiFunctions(authenticatedAccountId, CallMcpTool);
    }

    /// <summary>
    /// Create agent instructions tailored for portfolio analysis
    /// </summary>
    private string CreateAgentInstructions(int accountId)
    {
        try
        {
            return agentPromptService.GetPortfolioAdvisorPrompt(accountId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading agent instructions for account {AccountId}, using fallback", accountId);
            
            // Fallback prompt in case the service fails
            return $@"You are a friendly financial advisor helping the owner of Account ID {accountId}. 
            
Provide clear, helpful analysis of their portfolio data. Use conversational language rather than dry technical jargon. 
Format monetary amounts as £1,234.56 and use British date formats. Focus on actionable insights they can understand.

When they ask about portfolio data or market information, use your available tools to get current information.
For casual conversation, respond naturally without using tools.";
        }
    }

    /// <summary>
    /// Call an MCP tool through our local MCP server
    /// </summary>
    private async Task<object> CallMcpTool(string toolName, Dictionary<string, object> parameters)
    {
        try
        {
            logger.LogInformation("Calling MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);
            
            // Call our MCP server service directly (more efficient than HTTP calls)
            var result = await mcpServerService.ExecuteToolAsync(toolName, parameters);
            
            logger.LogInformation("Successfully executed MCP tool: {ToolName}", toolName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling MCP tool: {ToolName}", toolName);
            throw;
        }
    }




    /// <summary>
    /// Measure the estimated token count for context analysis
    /// </summary>
    private int MeasureContextSize(object content)
    {
        if (content == null) return 0;
        
        string text = content switch
        {
            string str => str,
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages => string.Join(" ", messages.Select(m => m.Text ?? "")),
            _ => content.ToString() ?? ""
        };
        
        // Rough estimation: ~4 characters per token
        return Math.Max(1, text.Length / 4);
    }

    /// <summary>
    /// Clean up markdown formatting issues in AI responses
    /// </summary>
    private string CleanupMarkdownFormatting(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        // Replace bullet symbols with dashes consistently
        var cleaned = response
            .Replace("• ", "- ")
            .Replace("◦ ", "- ")
            .Replace("▪ ", "- ");

        // Fix the specific issue: bullet point on separate line from content
        // Pattern: "•\nArticle:" becomes "- Article:"
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^[•\-]\s*\n([A-Za-z][^:\n]*:)",
            "- $1",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        // Fix standalone bullet points that are followed by content on next line
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^[•\-]\s*$\n([A-Za-z][^:\n]*:)",
            "- $1",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        // Clean up extra newlines that might be left behind
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\n\n\n+",
            "\n\n",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        return cleaned;
    }

    /// <summary>
    /// Setup memory-aware AI agent with optimized conversation history
    /// </summary>
    private async Task<(AIAgent agent, List<Microsoft.Extensions.AI.ChatMessage> messagesToSend)> SetupMemoryAwareAgent(string query, int accountId, int threadId, string? modelId, CancellationToken cancellationToken)
    {
        // Validate Azure Foundry configuration
        if (string.IsNullOrEmpty(azureFoundryOptions.Value.FoundryProjectEndpoint) || string.IsNullOrEmpty(azureFoundryOptions.Value.ApiKey))
        {
            throw new InvalidOperationException("Azure AI Foundry configuration is not valid. Ensure FoundryProjectEndpoint and ApiKey are configured.");
        }

        // Resolve model: use caller-supplied ID or fall back to configured default
        var effectiveModelId = modelId ?? azureFoundryOptions.Value.ModelName;
        logger.LogInformation("Using model {ModelId} for account {AccountId}, thread {ThreadId}", effectiveModelId, accountId, threadId);

        // Get a chat client for the selected model — OpenAI SDK routes all Foundry models via /openai/v1/
        var baseChatClient = _openAiClient.Value
            .GetChatClient(effectiveModelId)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "PortfolioManager.AI", 
                             configure: cfg => cfg.EnableSensitiveData = true) // Enable sensitive data for token metrics
            .Build();
        
        // Wrap with token tracking for detailed usage monitoring
        var tokenTrackingLogger = loggerFactory.CreateLogger<TokenTrackingChatClient>();
        var chatClient = new TokenTrackingChatClient(baseChatClient, tokenTrackingLogger, accountId, "portfolio-agent");
        
        // Create memory-aware AI agent with ChatClientAgentOptions and enhanced security
        // SECURITY: Pass authenticated accountId to tools to prevent cross-account access
        var portfolioTools = CreatePortfolioMcpFunctions(accountId);
        var secureInstructions = guardrails.CreateSecureAgentInstructions(CreateAgentInstructions(accountId), accountId);
        var secureChatOptions = guardrails.CreateSecureChatOptions(portfolioTools, accountId);
        
        // Set instructions on ChatOptions (moved from ChatClientAgentOptions in new Agent Framework API)
        secureChatOptions.Instructions = secureInstructions;
        
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = secureChatOptions,
            ChatHistoryProviderFactory = (ctx, ct) => new ValueTask<ChatHistoryProvider>(chatMessageStoreFactory(accountId, threadId, ctx.JsonSerializerOptions)),
            // Use existing context provider - optimization is in message loading instead
            AIContextProviderFactory = (ctx, ct) => new ValueTask<AIContextProvider>(memoryContextProviderFactory(accountId, chatClient))
        });

        logger.LogInformation("Memory store and context provider configured for agent on thread {ThreadId}", threadId);

        // OPTIMIZATION: Load existing conversation history with reduced token budget for faster processing
        var messageStore = chatMessageStoreFactory(accountId, threadId, null);
        
        // Use a smaller token budget for faster initial loading - prioritize recent context
        List<Microsoft.Extensions.AI.ChatMessage> recentMessages;
        
        if (messageStore is ITokenAwareChatMessageStore tokenAwareStore)
        {
            // OPTIMIZATION: Reduced from 2000 to 1000 tokens for 50% faster loading
            var tokenBudgetedMessages = await tokenAwareStore.GetMessagesWithinTokenBudgetAsync(
                tokenBudget: 1000, // Reduced for faster performance
                cancellationToken);
            recentMessages = tokenBudgetedMessages.ToList();
        }
        else
        {
            // Fallback: use empty message history when store doesn't support token-aware loading
            recentMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
        }

        // CONTEXT SIZE ANALYSIS: Measure each component for visibility
        var contextAnalysis = new
        {
            ConversationHistory = MeasureContextSize(recentMessages),
            AgentInstructions = MeasureContextSize(secureInstructions),
            ToolDescriptions = MeasureContextSize(secureChatOptions?.Tools?.Count.ToString() ?? "0 tools")
        };

        logger.LogInformation("Context Window Analysis for thread {ThreadId}: Conversation={ConversationTokens} tokens, Instructions={InstructionTokens} tokens, Tools={ToolTokens} tokens",
            threadId, 
            contextAnalysis.ConversationHistory, 
            contextAnalysis.AgentInstructions,
            contextAnalysis.ToolDescriptions);

        // Combine recent messages with new message using secure preparation
        var messagesToSend = await guardrails.PrepareSecureMessagesAsync(recentMessages, query, accountId, threadId);
        
        return (agent, messagesToSend);
    }

    /// <summary>
    /// Process portfolio query with streaming components
    /// </summary>
    private async Task ProcessWithStreamingComponents(string query, int accountId, int threadId, string? modelId, Func<StatusUpdateDto, Task>? onStatusUpdate, Func<string, Task> onTokenReceived, CancellationToken cancellationToken)
    {
        try
        {
            if (onStatusUpdate != null)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.ToolPlanning, "Setting up AI agent..."));
            }
            
            var (agent, messagesToSend) = await SetupMemoryAwareAgent(query, accountId, threadId, modelId, cancellationToken);
            
            logger.LogInformation("Processing with streaming agent for thread {ThreadId}", threadId);
            
            // Send initial thinking status
            if (onStatusUpdate != null)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Thinking, "Understanding your request..."));
            }
            
            var inToolCall = false;
            var contentStarted = false;
            var lastStatusUpdate = DateTime.UtcNow;
            var processingStartTime = DateTime.UtcNow;
            
            // Status rotation for long processing times
            var thinkingStatuses = new[]
            {
                "Processing your portfolio request...",
                "Gathering relevant information...",
                "Analyzing market context...",
                "Preparing detailed insights..."
            };
            var statusIndex = 0;
            
            // Use streaming response from the memory-aware agent
            await foreach (var streamingUpdate in agent.RunStreamingAsync(messagesToSend, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var now = DateTime.UtcNow;
                
                // Send periodic status updates if processing is taking a while and no content started
                if (onStatusUpdate != null && !contentStarted && 
                    (now - lastStatusUpdate).TotalSeconds >= 3)
                {
                    var processingDuration = (now - processingStartTime).TotalSeconds;
                    
                    if (processingDuration < 15) // First 15 seconds - rotate through thinking messages
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Thinking, thinkingStatuses[statusIndex]));
                        statusIndex = (statusIndex + 1) % thinkingStatuses.Length;
                    }
                    else // After 15 seconds - show we're working hard
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.GeneratingInsights, "Generating comprehensive analysis..."));
                    }
                    
                    lastStatusUpdate = now;
                }

                // Detect specific tool usage patterns for more specific updates
                if (onStatusUpdate != null && !contentStarted)
                {
                    if (streamingUpdate.Text?.Contains("get_portfolio_holdings") == true && !inToolCall)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.FetchingPortfolioData, "Retrieving your portfolio holdings..."));
                        inToolCall = true;
                        lastStatusUpdate = now;
                    }
                    else if (streamingUpdate.Text?.Contains("get_market_sentiment") == true && !inToolCall)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.FetchingMarketData, "Fetching market sentiment and news..."));
                        inToolCall = true;
                        lastStatusUpdate = now;
                    }
                    else if (streamingUpdate.Text?.Contains("analyze_performance") == true && !inToolCall)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.AnalyzingPerformance, "Analyzing portfolio performance..."));
                        inToolCall = true;
                        lastStatusUpdate = now;
                    }
                    else if (streamingUpdate.Text?.Contains("calculate_risk") == true && !inToolCall)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.AnalyzingRisk, "Calculating risk metrics..."));
                        inToolCall = true;
                        lastStatusUpdate = now;
                    }
                    
                    // Reset tool call flag when we get actual content
                    if (!string.IsNullOrWhiteSpace(streamingUpdate.Text) && 
                        !streamingUpdate.Text.Contains("get_") && 
                        !streamingUpdate.Text.Contains("analyze_") && 
                        !streamingUpdate.Text.Contains("calculate_") &&
                        inToolCall)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.GeneratingInsights, "Finalizing analysis..."));
                        inToolCall = false;
                        lastStatusUpdate = now;
                    }
                }

                if (!string.IsNullOrEmpty(streamingUpdate.Text))
                {
                    var cleanedText = CleanupMarkdownFormatting(streamingUpdate.Text);
                    
                    // Check if this looks like actual response content (not tool calls)
                    if (!contentStarted && 
                        !cleanedText.Contains("get_") && 
                        !cleanedText.Contains("analyze_") && 
                        !cleanedText.Contains("calculate_") &&
                        cleanedText.Trim().Length > 0 &&
                        !cleanedText.Contains("thinking") &&
                        !cleanedText.Contains("processing"))
                    {
                        contentStarted = true;
                    }
                    
                    await onTokenReceived(cleanedText);
                }
            }

            // Only send completion status if no content was streamed (meaning it was just tool execution)
            if (onStatusUpdate != null && !contentStarted)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Completed, "Analysis complete!"));
            }
            
            logger.LogInformation("Successfully completed streaming for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            if (onStatusUpdate != null)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Error, "An error occurred during processing"));
            }
            logger.LogError(ex, "Error in streaming processing for thread {ThreadId}", threadId);
            throw;
        }
    }


}
