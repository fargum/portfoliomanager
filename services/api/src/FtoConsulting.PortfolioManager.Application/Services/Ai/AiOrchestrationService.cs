using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Memory;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of AI orchestration service for portfolio queries
/// </summary>
public class AiOrchestrationService(
    ILogger<AiOrchestrationService> logger,
    IOptions<AzureFoundryOptions> azureFoundryOptions,
    IMcpServerService mcpServerService,
    IConversationThreadService conversationThreadService,
    IServiceProvider serviceProvider,
    IAgentPromptService agentPromptService,
    Func<int, int?, System.Text.Json.JsonSerializerOptions?, ChatMessageStore> chatMessageStoreFactory,
    Func<int, IChatClient, AIContextProvider> memoryContextProviderFactory) : IAiOrchestrationService
{
    private readonly ILogger<AiOrchestrationService> _logger = logger;
    private readonly AzureFoundryOptions _azureFoundryOptions = azureFoundryOptions.Value;
    private readonly IMcpServerService _mcpServerService = mcpServerService;
    private readonly IConversationThreadService _conversationThreadService = conversationThreadService;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IAgentPromptService _agentPromptService = agentPromptService;
    private readonly Func<int, int?, System.Text.Json.JsonSerializerOptions?, ChatMessageStore> _chatMessageStoreFactory = chatMessageStoreFactory;
    private readonly Func<int, IChatClient, AIContextProvider> _memoryContextProviderFactory = memoryContextProviderFactory;

    public async Task<ChatResponseDto> ProcessPortfolioQueryAsync(string query, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing portfolio query for account {AccountId}: {Query}", accountId, query);
            
            // Validate Azure Foundry configuration
            if (string.IsNullOrEmpty(_azureFoundryOptions.Endpoint))
            {
                throw new InvalidOperationException("Azure Foundry endpoint is not configured. Please check your user secrets.");
            }
            
            if (string.IsNullOrEmpty(_azureFoundryOptions.ApiKey))
            {
                throw new InvalidOperationException("Azure Foundry API key is not configured. Please check your user secrets.");
            }
            
            _logger.LogInformation("Azure Foundry Configuration - Endpoint: {Endpoint}, API Key Length: {ApiKeyLength}", 
                _azureFoundryOptions.Endpoint, _azureFoundryOptions.ApiKey.Length);

            // Create Azure OpenAI client
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(_azureFoundryOptions.Endpoint),
                new AzureKeyCredential(_azureFoundryOptions.ApiKey));

            // Get a chat client for the specific model
            var chatClient = azureOpenAIClient.GetChatClient(_azureFoundryOptions.ModelName);
            
            _logger.LogInformation("Created Azure OpenAI client and chat client successfully");
            
            // Create AI functions from our MCP tools for the agent
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create an AI agent with portfolio analysis instructions and MCP tools
            var agent = chatClient.CreateAIAgent(
                instructions: CreateAgentInstructions(accountId),
                tools: portfolioTools.ToList());

            // Process the query with the AI agent that has access to our MCP tools
            var currentDataDate = DateUtilities.GetPreviousWorkingDayForApi();
            var chatMessages = new[]
            {
                new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nData Available As Of: {currentDataDate} (use this date for 'current' or 'today' portfolio checks)")
            };
            
            _logger.LogInformation("Sending request to Azure OpenAI with {MessageCount} messages", chatMessages.Length);
            
            var response = await agent.RunAsync(chatMessages, cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully processed portfolio query using AI agent with MCP tools");

            var cleanedResponse = CleanupMarkdownFormatting(response.Text);

            return new ChatResponseDto(
                Response: cleanedResponse,
                QueryType: DetermineQueryType(query)
            );
        }
        catch (System.ClientModel.ClientResultException ex)
        {
            _logger.LogError(ex, "Azure OpenAI API error - Status: {Status}, Message: {Message}", ex.Status, ex.Message);
            
            return new ChatResponseDto(
                Response: $"I apologize, but I'm having trouble connecting to the AI service. Error: {ex.Message}. Please check your Azure OpenAI configuration.",
                QueryType: "Error"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing portfolio query for account {AccountId}: {Query}", accountId, query);
            
            return new ChatResponseDto(
                Response: "I apologize, but I encountered an issue analyzing your portfolio. Please ensure your account ID is correct and try again.",
                QueryType: "Error"
            );
        }
    }

    public async Task<ChatResponseDto> ProcessPortfolioQueryWithMemoryAsync(string query, int accountId, int? threadId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing portfolio query with memory for account {AccountId}, thread {ThreadId}: {Query}", 
                accountId, threadId, query);
            
            // Get or create conversation thread
            var thread = threadId.HasValue 
                ? await _conversationThreadService.GetThreadByIdAsync(threadId.Value, accountId, cancellationToken)
                : await _conversationThreadService.GetOrCreateActiveThreadAsync(accountId, cancellationToken);

            if (thread == null)
            {
                _logger.LogWarning("Could not get or create thread for account {AccountId}", accountId);
                // Fallback to non-memory version
                return await ProcessPortfolioQueryAsync(query, accountId, cancellationToken);
            }

            // Try to use memory-aware processing
            try
            {
                var response = await ProcessWithMemoryComponents(query, accountId, thread.Id, cancellationToken);
                return response with { 
                    ThreadId = thread.Id, 
                    ThreadTitle = thread.ThreadTitle 
                };
            }
            catch (Exception memoryEx)
            {
                _logger.LogWarning(memoryEx, "Memory components failed, falling back to standard processing");
                var response = await ProcessPortfolioQueryAsync(query, accountId, cancellationToken);
                return response with { 
                    ThreadId = thread.Id, 
                    ThreadTitle = thread.ThreadTitle 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing portfolio query with memory for account {AccountId}", accountId);
            
            return new ChatResponseDto(
                Response: "I apologize, but I encountered an issue analyzing your portfolio. Please ensure your account ID is correct and try again.",
                QueryType: "Error"
            );
        }
    }

    public async Task ProcessPortfolioQueryStreamWithMemoryAsync(string query, int accountId, Func<string, Task> onTokenReceived, int? threadId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing streaming portfolio query with memory for account {AccountId}, thread {ThreadId}: {Query}", 
                accountId, threadId, query);
            
            // Get or create conversation thread
            var thread = threadId.HasValue 
                ? await _conversationThreadService.GetThreadByIdAsync(threadId.Value, accountId, cancellationToken)
                : await _conversationThreadService.GetOrCreateActiveThreadAsync(accountId, cancellationToken);

            if (thread == null)
            {
                _logger.LogWarning("Could not get or create thread for account {AccountId}", accountId);
                await ProcessPortfolioQueryStreamAsync(query, accountId, onTokenReceived, cancellationToken);
                return;
            }

            // Try to use memory-aware streaming
            try
            {
                await ProcessStreamingWithMemoryComponents(query, accountId, thread.Id, onTokenReceived, cancellationToken);
            }
            catch (Exception memoryEx)
            {
                _logger.LogWarning(memoryEx, "Memory components failed, falling back to standard streaming");
                await ProcessPortfolioQueryStreamAsync(query, accountId, onTokenReceived, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming portfolio query with memory for account {AccountId}", accountId);
            await onTokenReceived("I apologize, but I encountered an issue analyzing your portfolio. Please ensure your account ID is correct and try again.");
        }
    }

    public async Task ProcessPortfolioQueryStreamAsync(string query, int accountId, Func<string, Task> onTokenReceived, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing streaming portfolio query for account {AccountId}: {Query}", accountId, query);
            
            // Validate Azure Foundry configuration
            if (string.IsNullOrEmpty(_azureFoundryOptions.Endpoint))
            {
                throw new InvalidOperationException("Azure Foundry endpoint is not configured. Please check your user secrets.");
            }
            
            if (string.IsNullOrEmpty(_azureFoundryOptions.ApiKey))
            {
                throw new InvalidOperationException("Azure Foundry API key is not configured. Please check your user secrets.");
            }

            // Create Azure OpenAI client
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(_azureFoundryOptions.Endpoint),
                new AzureKeyCredential(_azureFoundryOptions.ApiKey));

            // Get a chat client for the specific model
            var chatClient = azureOpenAIClient.GetChatClient(_azureFoundryOptions.ModelName);
            
            _logger.LogInformation("Created Azure OpenAI client and chat client successfully for streaming");
            
            // Create AI functions from our MCP tools for the agent
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create an AI agent with portfolio analysis instructions and MCP tools
            var agent = chatClient.CreateAIAgent(
                instructions: CreateAgentInstructions(accountId),
                tools: portfolioTools.ToList());

            // Process the query with streaming using the AI agent
            var currentDataDate = DateUtilities.GetPreviousWorkingDayForApi();
            var chatMessages = new[]
            {
                new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nData Available As Of: {currentDataDate} (use this date for 'current' or 'today' portfolio checks)")
            };
            
            _logger.LogInformation("Sending streaming request to Azure OpenAI with {MessageCount} messages", chatMessages.Length);
            
            // Use streaming response from the agent
            await foreach (var streamingUpdate in agent.RunStreamingAsync(chatMessages, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(streamingUpdate.Text))
                {
                    // Apply comprehensive markdown cleanup for streaming responses
                    var cleanedText = CleanupMarkdownFormatting(streamingUpdate.Text);
                    
                    await onTokenReceived(cleanedText);
                }
            }

            _logger.LogInformation("Successfully completed streaming portfolio query using AI agent with MCP tools");
        }
        catch (System.ClientModel.ClientResultException ex)
        {
            _logger.LogError(ex, "Azure OpenAI API error in streaming - Status: {Status}, Message: {Message}", ex.Status, ex.Message);
            await onTokenReceived($"I apologize, but I'm having trouble connecting to the AI service. Error: {ex.Message}. Please check your Azure OpenAI configuration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming portfolio query for account {AccountId}: {Query}", accountId, query);
            await onTokenReceived("I apologize, but I encountered an issue analyzing your portfolio. Please ensure your account ID is correct and try again.");
        }
    }

    public async Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync()
    {
        // Tool definitions are now managed by the centralized PortfolioToolRegistry
        // This provides a summary view for the AI orchestration service
        _logger.LogInformation("Returning tool summary from centralized portfolio tool registry");
        
        await Task.CompletedTask;
        return PortfolioToolRegistry.GetAiToolDtos();
    }

    /// <summary>
    /// Create AI functions that connect to our MCP server tools
    /// </summary>
    private IEnumerable<AITool> CreatePortfolioMcpFunctions()
    {
        // Create AI functions from the centralized tool registry
        // These will be used by the AI agent to call our MCP tools
        return PortfolioToolRegistry.CreateAiFunctions(CallMcpTool);
    }

    /// <summary>
    /// Create agent instructions tailored for portfolio analysis
    /// </summary>
    private string CreateAgentInstructions(int accountId)
    {
        try
        {
            return _agentPromptService.GetPortfolioAdvisorPrompt(accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading agent instructions for account {AccountId}, using fallback", accountId);
            
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
            _logger.LogInformation("Calling MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);
            
            // Call our MCP server service directly (more efficient than HTTP calls)
            var result = await _mcpServerService.ExecuteToolAsync(toolName, parameters);
            
            _logger.LogInformation("Successfully executed MCP tool: {ToolName}", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool: {ToolName}", toolName);
            throw;
        }
    }

    /// <summary>
    /// Determine the type of query being asked
    /// </summary>
    private string DetermineQueryType(string query)
    {
        var queryLower = query.ToLowerInvariant();

        return queryLower switch
        {
            var q when q.Contains("performance") || q.Contains("return") || q.Contains("gain") || q.Contains("loss") => "Performance",
            var q when q.Contains("holding") || q.Contains("position") || q.Contains("stock") || q.Contains("what do i own") => "Holdings",
            var q when q.Contains("market") || q.Contains("news") || q.Contains("sentiment") => "Market",
            var q when q.Contains("risk") || q.Contains("diversification") || q.Contains("concentration") => "Risk", 
            var q when q.Contains("compare") || q.Contains("vs") || q.Contains("versus") || q.Contains("between") => "Comparison",
            _ => "General"
        };
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
    /// Process portfolio query with Microsoft Agent Framework memory components
    /// </summary>
    private async Task<ChatResponseDto> ProcessWithMemoryComponents(string query, int accountId, int threadId, CancellationToken cancellationToken)
    {
        try
        {
            // Validate Azure Foundry configuration
            if (string.IsNullOrEmpty(_azureFoundryOptions.Endpoint) || string.IsNullOrEmpty(_azureFoundryOptions.ApiKey))
            {
                throw new InvalidOperationException("Azure Foundry configuration is not valid for memory processing.");
            }

            // Create Azure OpenAI client
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(_azureFoundryOptions.Endpoint),
                new AzureKeyCredential(_azureFoundryOptions.ApiKey));

            // Get a chat client for the specific model
            var chatClient = azureOpenAIClient.GetChatClient(_azureFoundryOptions.ModelName);
            
            // Create AI functions from our MCP tools
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create memory-aware AI agent with ChatClientAgentOptions
            var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
            {
                Instructions = CreateAgentInstructions(accountId),
                ChatOptions = new ChatOptions { Tools = portfolioTools.ToList() },
                ChatMessageStoreFactory = ctx => _chatMessageStoreFactory(accountId, threadId, ctx.JsonSerializerOptions),
                AIContextProviderFactory = ctx => _memoryContextProviderFactory(accountId, chatClient.AsIChatClient())
            });

            _logger.LogInformation("Memory store and context provider configured for agent on thread {ThreadId}", threadId);

            // Load existing conversation history from the chat message store
            var messageStore = _chatMessageStoreFactory(accountId, threadId, null);
            var existingMessages = await messageStore.GetMessagesAsync(cancellationToken);
            var conversationHistory = existingMessages.ToList();

            // Add the new user query to the conversation
            var currentDataDate = DateUtilities.GetPreviousWorkingDayForApi();
            var newUserMessage = new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nThread ID: {threadId}\nData Available As Of: {currentDataDate} (use this date for 'current' or 'today' portfolio checks)");
            conversationHistory.Add(newUserMessage);

            _logger.LogInformation("Loaded {HistoryCount} previous messages for thread {ThreadId}, processing with full conversation context", 
                existingMessages.Count(), threadId);
            
            _logger.LogInformation("Processing with memory-aware agent for thread {ThreadId}", threadId);
            
            // Run the agent with full conversation history
            var response = await agent.RunAsync(conversationHistory, cancellationToken: cancellationToken);

            var cleanedResponse = CleanupMarkdownFormatting(response.Text);

            _logger.LogInformation("Successfully processed portfolio query with memory for thread {ThreadId}", threadId);

            return new ChatResponseDto(
                Response: cleanedResponse,
                QueryType: DetermineQueryType(query)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in memory-aware processing for thread {ThreadId}", threadId);
            throw;
        }
    }

    /// <summary>
    /// Process streaming portfolio query with Microsoft Agent Framework memory components
    /// </summary>
    private async Task ProcessStreamingWithMemoryComponents(string query, int accountId, int threadId, Func<string, Task> onTokenReceived, CancellationToken cancellationToken)
    {
        try
        {
            // Validate Azure Foundry configuration
            if (string.IsNullOrEmpty(_azureFoundryOptions.Endpoint) || string.IsNullOrEmpty(_azureFoundryOptions.ApiKey))
            {
                throw new InvalidOperationException("Azure Foundry configuration is not valid for memory processing.");
            }

            // Create Azure OpenAI client
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(_azureFoundryOptions.Endpoint),
                new AzureKeyCredential(_azureFoundryOptions.ApiKey));

            // Get a chat client for the specific model
            var chatClient = azureOpenAIClient.GetChatClient(_azureFoundryOptions.ModelName);
            
            // Create AI functions from our MCP tools
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create memory-aware AI agent with ChatClientAgentOptions
            var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
            {
                Instructions = CreateAgentInstructions(accountId),
                ChatOptions = new ChatOptions { Tools = portfolioTools.ToList() },
                ChatMessageStoreFactory = ctx => _chatMessageStoreFactory(accountId, threadId, ctx.JsonSerializerOptions),
                AIContextProviderFactory = ctx => _memoryContextProviderFactory(accountId, chatClient.AsIChatClient())
            });

            _logger.LogInformation("Memory store and context provider configured for streaming agent on thread {ThreadId}", threadId);

            // Load existing conversation history from the chat message store
            var messageStore = _chatMessageStoreFactory(accountId, threadId, null);
            var existingMessages = await messageStore.GetMessagesAsync(cancellationToken);
            var conversationHistory = existingMessages.ToList();

            // Add the new user query to the conversation
            var currentDataDate = DateUtilities.GetPreviousWorkingDayForApi();
            var newUserMessage = new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nThread ID: {threadId}\nData Available As Of: {currentDataDate} (use this date for 'current' or 'today' portfolio checks)");
            conversationHistory.Add(newUserMessage);

            _logger.LogInformation("Loaded {HistoryCount} previous messages for streaming on thread {ThreadId}", 
                existingMessages.Count(), threadId);
            
            _logger.LogInformation("Processing streaming with memory-aware agent for thread {ThreadId}", threadId);
            
            // Use streaming response from the memory-aware agent with full conversation history
            await foreach (var streamingUpdate in agent.RunStreamingAsync(conversationHistory, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(streamingUpdate.Text))
                {
                    var cleanedText = CleanupMarkdownFormatting(streamingUpdate.Text);
                    await onTokenReceived(cleanedText);
                }
            }

            _logger.LogInformation("Successfully completed streaming with memory for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in memory-aware streaming processing for thread {ThreadId}", threadId);
            throw;
        }
    }

 }