using FtoConsulting.PortfolioManager.Application.Utilities;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
    IConversationThreadService conversationThreadService,
    IAgentPromptService agentPromptService,
    AgentFrameworkGuardrails guardrails,
    Func<int, int?, ChatHistoryProvider> chatMessageStoreFactory,
    Func<int, IChatClient, AIContextProvider> memoryContextProviderFactory,
    ILoggerFactory loggerFactory,
    IServiceScopeFactory serviceScopeFactory,
    Func<string, IChatClient>? modelClientFactory = null) : IAiOrchestrationService
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AI");

    // Lazy-loaded OpenAI client pointing to the Foundry /openai/v1/ endpoint — works for all deployed models
    private readonly Lazy<OpenAIClient> _openAiClient = new Lazy<OpenAIClient>(() =>
        new OpenAIClient(
            new ApiKeyCredential(azureFoundryOptions.Value.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(azureFoundryOptions.Value.FoundryProjectEndpoint),
                NetworkTimeout = TimeSpan.FromSeconds(azureFoundryOptions.Value.TimeoutSeconds)
            }));

    public async Task ProcessPortfolioQueryAsync(string query, int accountId, Func<StatusUpdateDto, Task>? onStatusUpdate,
        Func<string, Task> onTokenReceived, int? threadId = null, string? modelId = null, bool storeInHistory = true,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecutePortfolioQueryAsync(query, accountId, new AgentExecutionOptions(), onStatusUpdate,
            onTokenReceived, threadId, modelId, storeInHistory, cancellationToken);
        if (result.Status == AgentExecutionStatus.Cancelled) cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<AgentExecution> ExecutePortfolioQueryAsync(
        string query,
        int accountId,
        AgentExecutionOptions options,
        Func<StatusUpdateDto, Task>? onStatusUpdate = null,
        Func<string, Task>? onTokenReceived = null,
        int? threadId = null,
        string? modelId = null,
        bool storeInHistory = true,
        CancellationToken cancellationToken = default)
    {
        var capture = new AgentExecutionCapture(options);
        var result = capture.Execution;
        result.Model = modelId ?? azureFoundryOptions.Value.ModelName;
        result.ConversationId ??= Guid.NewGuid().ToString("N");
        if (options.CaptureContent) result.Query = query;
        using var clock = PortfolioClock.Use(options.EffectiveTime);
        using var activity = s_activitySource.StartActivity("portfolio.query");
        result.TraceId = Activity.Current?.TraceId.ToHexString();
        Activity.Current?.SetTag("portfolio.execution.id", result.ExecutionId);
        Activity.Current?.SetTag("gen_ai.agent.id", result.AgentId);
        Activity.Current?.SetTag("gen_ai.conversation.id", result.ConversationId);
        Activity.Current?.SetTag("evaluation.run.id", options.SuiteRunId);
        Activity.Current?.SetTag("evaluation.scenario.id", options.ScenarioId);
        using var correlation = new AgentTraceCorrelation(result);
        var text = options.CaptureContent ? new System.Text.StringBuilder() : null;
        async Task Token(string token)
        {
            result.FirstContentAt ??= DateTimeOffset.UtcNow;
            text?.Append(token);
            if (onTokenReceived != null) await onTokenReceived(token);
        }
        storeInHistory = storeInHistory && options.ContextMode == AgentContextMode.Production;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // When storeInHistory=false (e.g. automated reports) we never touch the user's conversation thread
            int resolvedThreadId;
            if (storeInHistory)
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
                resolvedThreadId = thread.Id;
                result.ThreadId = thread.Id;
                result.ConversationId = options.ConversationId ?? $"portfolio-thread-{thread.Id}";
                Activity.Current?.SetTag("thread.id", thread.Id);
                Activity.Current?.SetTag("gen_ai.conversation.id", result.ConversationId);
            }
            else
            {
                // Use a sentinel value — history will not be loaded or persisted
                resolvedThreadId = -1;
                logger.LogInformation("Report query for account {AccountId} running in ephemeral mode (no history storage)", accountId);
            }

            // GUARDRAILS: Validate input
            var inputValidation = await guardrails.ValidateInputAsync(query, accountId);

            if (!inputValidation.IsValid)
            {
                await guardrails.LogSecurityIncident(inputValidation, accountId, "ProcessPortfolioQueryAsync");

                var fallbackResponse = guardrails.CreateFallbackResponse(inputValidation, accountId);
                await Token(fallbackResponse);
                result.Status = AgentExecutionStatus.Blocked;
                return result;
            }

            await ProcessWithStreamingComponents(
                query,
                accountId,
                resolvedThreadId,
                modelId,
                storeInHistory,
                onStatusUpdate,
                Token,
                cancellationToken,
                capture
            );
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Status == AgentExecutionStatus.Running) result.Status = AgentExecutionStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Status = AgentExecutionStatus.Cancelled;
            result.ErrorType = typeof(OperationCanceledException).FullName;
        }
        catch (Exception ex)
        {
            result.Status = AgentExecutionStatus.Failed;
            result.ErrorType = ex.GetType().FullName;
            logger.LogError(ex, "Error processing portfolio query for account {AccountId}", accountId);

            var errorResponse = "I apologize, but I encountered an error while processing your request. Please try again.";
            try { await Token(errorResponse); }
            catch (Exception callbackError)
            {
                logger.LogWarning(callbackError, "Could not deliver failure response for execution {ExecutionId}", result.ExecutionId);
            }
        }
        finally
        {
            result.CompletedAt = DateTimeOffset.UtcNow;
            result.Response = text?.ToString();
            activity?.SetTag("portfolio.execution.status", result.Status.ToString());
            if (result.Status is AgentExecutionStatus.Failed or AgentExecutionStatus.Cancelled)
                activity?.SetStatus(ActivityStatusCode.Error, result.Status.ToString());
        }
        return result;
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
    private IEnumerable<AITool> CreatePortfolioMcpFunctions(int authenticatedAccountId, AgentExecutionCapture capture)
    {
        // Create AI functions from the centralized tool registry
        // These will be used by the AI agent to call our MCP tools
        // Pass the authenticated account ID to prevent cross-account data access
        return PortfolioToolRegistry.CreateAiFunctions(authenticatedAccountId, (name, args, ct) => CallMcpTool(name, args, ct, capture));
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
    /// Call an MCP tool through our local MCP server.
    /// Each call gets its own DI scope so concurrent invocations have isolated DbContext
    /// instances and do not race on the same EF Core context.
    /// </summary>
    private async Task<object> CallMcpTool(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken, AgentExecutionCapture capture)
    {
        if (capture.Options.CaptureContent && FunctionInvokingChatClient.CurrentContext is { } context)
            capture.RecordExecutionArguments(context.CallContent.CallId, parameters);
        // Create a dedicated scope for this tool call so that the DbContext it resolves
        // is never shared with a concurrently-running tool call on another thread.
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var scopedMcpService = scope.ServiceProvider.GetRequiredService<IMcpServerService>();

        try
        {
            logger.LogInformation("Calling MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);

            var result = await scopedMcpService.ExecuteToolAsync(toolName, parameters, cancellationToken);

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
    /// Setup memory-aware AI agent with optimized conversation history
    /// </summary>
    private async Task<(AIAgent agent, List<Microsoft.Extensions.AI.ChatMessage> messagesToSend)> SetupMemoryAwareAgent(string query, int accountId, int threadId, string? modelId, bool storeInHistory, CancellationToken cancellationToken, AgentExecutionCapture capture)
    {
        // Validate Azure Foundry configuration
        if (modelClientFactory == null && (string.IsNullOrEmpty(azureFoundryOptions.Value.FoundryProjectEndpoint) || string.IsNullOrEmpty(azureFoundryOptions.Value.ApiKey)))
        {
            throw new InvalidOperationException("Azure AI Foundry configuration is not valid. Ensure FoundryProjectEndpoint and ApiKey are configured.");
        }

        // Resolve model: use caller-supplied ID or fall back to configured default
        var effectiveModelId = modelId ?? azureFoundryOptions.Value.ModelName;
        logger.LogInformation("Using model {ModelId} for account {AccountId}, thread {ThreadId}", effectiveModelId, accountId, threadId);

        // Get a chat client for the selected model — OpenAI SDK routes all Foundry models via /openai/v1/
        var modelClient = modelClientFactory?.Invoke(effectiveModelId) ?? _openAiClient.Value.GetChatClient(effectiveModelId).AsIChatClient();
        var baseChatClient = modelClient
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory: loggerFactory, configure: c =>
            {
                c.AllowConcurrentInvocation = true;
                if (capture.Options.CaptureContent) c.FunctionInvoker = capture.InvokeFunctionAsync;
            })
            .Use(inner => capture.Options.CaptureContent ? new ExecutionRecordingChatClient(inner, capture) : inner)
            .UseOpenTelemetry(sourceName: "PortfolioManager.AI",
                             configure: cfg => cfg.EnableSensitiveData = capture.Options.CaptureContent || azureFoundryOptions.Value.CaptureSensitiveTelemetry)
            .Build();

        // Wrap with token tracking for detailed usage monitoring
        var tokenTrackingLogger = loggerFactory.CreateLogger<TokenTrackingChatClient>();
        var chatClient = new TokenTrackingChatClient(baseChatClient, tokenTrackingLogger, accountId, "portfolio-agent");

        // Create memory-aware AI agent with ChatClientAgentOptions and enhanced security
        // SECURITY: Pass authenticated accountId to tools to prevent cross-account access
        // Some models (e.g. Llama, Phi via vLLM) don't support tool calling — skip tools for those
        var modelConfig = azureFoundryOptions.Value.AvailableModels.FirstOrDefault(m => m.Id == effectiveModelId);
        var modelSupportsTools = modelConfig?.SupportsTools ?? true;
        var portfolioTools = modelSupportsTools ? CreatePortfolioMcpFunctions(accountId, capture) : [];
        if (!modelSupportsTools)
            logger.LogInformation("Model {ModelId} does not support tool calling — running without MCP tools", effectiveModelId);
        var secureInstructions = guardrails.CreateSecureAgentInstructions(CreateAgentInstructions(accountId), accountId);
        var secureChatOptions = guardrails.CreateSecureChatOptions(portfolioTools, accountId);

        // Set instructions on ChatOptions — inject current date so model knows what "today" means.
        // This is ephemeral (not stored in history) and refreshed on every call.
        var currentDate = PortfolioClock.UtcNow.ToString("yyyy-MM-dd");
        secureChatOptions.Instructions = secureInstructions + $"\n\nCurrent Date: {currentDate}";

        // Build a compaction pipeline to manage context window efficiently:
        // 1. ToolResultCompactionStrategy — compacts verbose MCP tool result payloads (holdings JSON, market data) into YAML summaries
        // 2. SlidingWindowCompactionStrategy — when conversation exceeds 10 turns, oldest turns are dropped.
        //    This is a lightweight, zero-latency approach that avoids the extra LLM call that
        //    SummarizationCompactionStrategy requires (which adds ~60s and leaks stale context).
        var compactionProvider = new CompactionProvider(
            new PipelineCompactionStrategy([
                new ToolResultCompactionStrategy(CompactionTriggers.HasToolCalls()),
                new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(10), minimumPreservedTurns: 5)
            ]),
            stateKey: "portfolio-compaction",
            loggerFactory: loggerFactory);

        capture.Execution.PromptHash = AgentExecutionCapture.Hash(secureChatOptions.Instructions);
        if (capture.Options.CaptureContent)
        {
            capture.Execution.Instructions = secureChatOptions.Instructions;
            capture.Execution.ToolDefinitions.AddRange(secureChatOptions.Tools!.OfType<AIFunction>().Select(f =>
                new ToolDefinitionSnapshot(f.Name, f.Description, f.JsonSchema.Clone())));
            capture.Execution.ToolSchemaHash = AgentExecutionCapture.Hash(System.Text.Json.JsonSerializer.Serialize(capture.Execution.ToolDefinitions));
        }
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Id = capture.Execution.AgentId,
            Name = "portfolio-manager",
            ChatOptions = secureChatOptions,
            UseProvidedChatClientAsIs = true, // We add our own FunctionInvokingChatClient with AllowConcurrentInvocation=true
            ChatHistoryProvider = storeInHistory
                ? chatMessageStoreFactory(accountId, threadId)
                : new EphemeralChatHistoryProvider(),
            AIContextProviders = capture.Options.ContextMode == AgentContextMode.Production
                ? [memoryContextProviderFactory(accountId, chatClient), compactionProvider]
                : [compactionProvider]
        }).AsBuilder().UseOpenTelemetry(sourceName: "PortfolioManager.AI",
            configure: cfg => cfg.EnableSensitiveData = capture.Options.CaptureContent || azureFoundryOptions.Value.CaptureSensitiveTelemetry).Build();

        logger.LogInformation("Memory store and compaction provider configured for agent on thread {ThreadId} (storeInHistory={StoreInHistory})", threadId, storeInHistory);

        // Prepare the user query — conversation history is injected by ChatHistoryProvider.InvokingCoreAsync,
        // memory context by PortfolioMemoryContextProvider, and compaction by CompactionProvider.
        // We only send the new user message here to avoid double-loading history.
        var messagesToSend = await guardrails.PrepareSecureMessagesAsync(capture.Options.ContextMode == AgentContextMode.Seeded ? capture.Options.SeedMessages : [], query, accountId, threadId);

        return (agent, messagesToSend);
    }

    /// <summary>
    /// Process portfolio query with streaming components
    /// </summary>
    private async Task ProcessWithStreamingComponents(string query, int accountId, int threadId, string? modelId, bool storeInHistory, Func<StatusUpdateDto, Task>? onStatusUpdate, Func<string, Task> onTokenReceived, CancellationToken cancellationToken, AgentExecutionCapture capture)
    {
        try
        {
            if (onStatusUpdate != null)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.ToolPlanning, "Setting up AI agent..."));
            }

            var (agent, messagesToSend) = await SetupMemoryAwareAgent(query, accountId, threadId, modelId, storeInHistory, cancellationToken, capture);

            logger.LogInformation("Processing with streaming agent for thread {ThreadId}", threadId);

            // Send initial thinking status
            if (onStatusUpdate != null)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Thinking, "Understanding your request..."));
            }

            var contentStarted = false;
            ChatFinishReason? lastFinishReason = null;
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
                cancellationToken.ThrowIfCancellationRequested();

                if (streamingUpdate.FinishReason is { } reason)
                    lastFinishReason = reason;

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

                // Track whether the model is invoking tools — FunctionCallContent appears in
                // the streaming update's Contents collection when the framework is executing tools.
                if (!contentStarted && streamingUpdate.Contents.OfType<FunctionCallContent>().Any())
                {
                    if (onStatusUpdate != null)
                    {
                        await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.FetchingPortfolioData, "Retrieving data from your portfolio..."));
                        lastStatusUpdate = now;
                    }
                }

                if (!string.IsNullOrEmpty(streamingUpdate.Text))
                {
                    if (!contentStarted)
                        contentStarted = true;

                    await onTokenReceived(streamingUpdate.Text);
                }
            }

            // Only send completion status if no content was streamed (meaning it was just tool execution)
            if (onStatusUpdate != null && !contentStarted)
            {
                await onStatusUpdate(new StatusUpdateDto(StatusUpdateType.Completed, "Analysis complete!"));
            }

            // Check the last streaming update's FinishReason to detect truncated or filtered responses
            if (lastFinishReason == ChatFinishReason.Length)
            {
                capture.Execution.Status = AgentExecutionStatus.Truncated;
                logger.LogWarning("Response was truncated (FinishReason=Length) for thread {ThreadId}", threadId);
                await onTokenReceived("\n\n⚠️ *My response was cut short because it hit the model's output limit. Ask me to continue if you need more detail.*");
            }
            else if (lastFinishReason == ChatFinishReason.ContentFilter)
            {
                capture.Execution.Status = AgentExecutionStatus.Filtered;
                logger.LogWarning("Response was filtered (FinishReason=ContentFilter) for thread {ThreadId}", threadId);
                await onTokenReceived("\n\n⚠️ *Part of my response was filtered by the content safety system. Please rephrase your question if needed.*");
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

/// <summary>
/// A no-op ChatHistoryProvider used for automated reports and other ephemeral AI calls
/// that must not pollute the user's active conversation history.
/// Returns no history — the base class merges with context.RequestMessages automatically.
/// </summary>
file sealed class EphemeralChatHistoryProvider : ChatHistoryProvider
{
    protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    protected override ValueTask<IEnumerable<Microsoft.Extensions.AI.ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
        => new(Enumerable.Empty<Microsoft.Extensions.AI.ChatMessage>());
}
