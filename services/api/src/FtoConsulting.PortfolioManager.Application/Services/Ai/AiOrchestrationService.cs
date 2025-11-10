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
public class AiOrchestrationService : IAiOrchestrationService
{
    private readonly ILogger<AiOrchestrationService> _logger;
    private readonly AzureFoundryOptions _azureFoundryOptions;
    private readonly IMcpServerService _mcpServerService;
    private readonly IConversationThreadService _conversationThreadService;
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<int, int?, System.Text.Json.JsonSerializerOptions?, ChatMessageStore> _chatMessageStoreFactory;
    private readonly Func<int, IChatClient, AIContextProvider> _memoryContextProviderFactory;

    public AiOrchestrationService(
        ILogger<AiOrchestrationService> logger,
        IOptions<AzureFoundryOptions> azureFoundryOptions,
        IMcpServerService mcpServerService,
        IConversationThreadService conversationThreadService,
        IServiceProvider serviceProvider,
        Func<int, int?, System.Text.Json.JsonSerializerOptions?, ChatMessageStore> chatMessageStoreFactory,
        Func<int, IChatClient, AIContextProvider> memoryContextProviderFactory)
    {
        _logger = logger;
        _azureFoundryOptions = azureFoundryOptions.Value;
        _mcpServerService = mcpServerService;
        _conversationThreadService = conversationThreadService;
        _serviceProvider = serviceProvider;
        _chatMessageStoreFactory = chatMessageStoreFactory;
        _memoryContextProviderFactory = memoryContextProviderFactory;
    }

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
        // Tool definitions are now managed by the Microsoft Agent Framework MCP server
        // This method provides a summary view for the AI orchestration service
        _logger.LogInformation("Returning tool summary from unified MCP architecture");
        
        await Task.CompletedTask;

        return new[]
        {
            new AiToolDto(
                Name: "GetPortfolioHoldings",
                Description: "Retrieve portfolio holdings for a specific account and date",
                Parameters: new Dictionary<string, object>
                {
                    ["accountId"] = new { type = "integer", description = "Account ID" },
                    ["date"] = new { type = "string", description = "Date in YYYY-MM-DD format" }
                },
                Category: "Portfolio Data"
            ),
            new AiToolDto(
                Name: "AnalyzePortfolioPerformance",
                Description: "Analyze portfolio performance and generate insights for a specific date",
                Parameters: new Dictionary<string, object>
                {
                    ["accountId"] = new { type = "integer", description = "Account ID" },
                    ["analysisDate"] = new { type = "string", description = "Analysis date in YYYY-MM-DD format" }
                },
                Category: "Portfolio Analysis"
            ),
            new AiToolDto(
                Name: "ComparePortfolioPerformance",
                Description: "Compare portfolio performance between two dates",
                Parameters: new Dictionary<string, object>
                {
                    ["accountId"] = new { type = "integer", description = "Account ID" },
                    ["startDate"] = new { type = "string", description = "Start date in YYYY-MM-DD format" },
                    ["endDate"] = new { type = "string", description = "End date in YYYY-MM-DD format" }
                },
                Category: "Portfolio Analysis"
            ),
            new AiToolDto(
                Name: "GetMarketContext",
                Description: "Get market context and news for specific stock tickers",
                Parameters: new Dictionary<string, object>
                {
                    ["tickers"] = new { type = "array", description = "List of stock tickers" },
                    ["date"] = new { type = "string", description = "Date for market analysis in YYYY-MM-DD format" }
                },
                Category: "Market Intelligence"
            ),
            new AiToolDto(
                Name: "SearchFinancialNews",
                Description: "Search for financial news related to specific tickers within a date range",
                Parameters: new Dictionary<string, object>
                {
                    ["tickers"] = new { type = "array", description = "List of stock tickers" },
                    ["fromDate"] = new { type = "string", description = "Start date in YYYY-MM-DD format" },
                    ["toDate"] = new { type = "string", description = "End date in YYYY-MM-DD format" }
                },
                Category: "Market Intelligence"
            ),
            new AiToolDto(
                Name: "GetMarketSentiment",
                Description: "Get overall market sentiment and indicators for a specific date",
                Parameters: new Dictionary<string, object>
                {
                    ["date"] = new { type = "string", description = "Date for sentiment analysis in YYYY-MM-DD format" }
                },
                Category: "Market Intelligence"
            )
        };
    }

    /// <summary>
    /// Create AI functions that connect to our MCP server tools
    /// </summary>
    private IEnumerable<AITool> CreatePortfolioMcpFunctions()
    {
        // Create AI functions that map to our MCP server endpoints
        // These will be used by the AI agent to call our MCP tools
        var functions = new List<AITool>
        {
            AIFunctionFactory.Create(
                method: (int accountId, string date) => CallMcpTool("GetPortfolioHoldings", new { accountId, date }),
                name: "GetPortfolioHoldings",
                description: "Retrieve portfolio holdings for a specific account and date"),

            AIFunctionFactory.Create(
                method: (int accountId, string analysisDate) => CallMcpTool("AnalyzePortfolioPerformance", new { accountId, analysisDate }),
                name: "AnalyzePortfolioPerformance",
                description: "Analyze portfolio performance and generate insights for a specific date"),

            AIFunctionFactory.Create(
                method: (int accountId, string startDate, string endDate) => CallMcpTool("ComparePortfolioPerformance", new { accountId, startDate, endDate }),
                name: "ComparePortfolioPerformance",
                description: "Compare portfolio performance between two dates"),

            AIFunctionFactory.Create(
                method: (string[] tickers, string date) => CallMcpTool("GetMarketContext", new { tickers, date }),
                name: "GetMarketContext",
                description: "Get market context and news for specific stock tickers"),

            AIFunctionFactory.Create(
                method: (string[] tickers, string fromDate, string toDate) => CallMcpTool("SearchFinancialNews", new { tickers, fromDate, toDate }),
                name: "SearchFinancialNews",
                description: "Search for financial news related to specific tickers within a date range"),

            AIFunctionFactory.Create(
                method: (string date) => CallMcpTool("GetMarketSentiment", new { date }),
                name: "GetMarketSentiment",
                description: "Get overall market sentiment and indicators for a specific date")
        };

        return functions;
    }

    /// <summary>
    /// Create agent instructions tailored for portfolio analysis
    /// </summary>
    private string CreateAgentInstructions(int accountId)
    {
        return $@"You are a friendly financial advisor helping the owner of Account ID {accountId}. Think of yourself as their personal finance assistant - knowledgeable but approachable, professional but not stuffy.

WHEN TO USE YOUR TOOLS:
You have some great tools at your disposal, but only use them when someone actually wants portfolio or market information:

✅ Perfect times to use tools:
- ""Show me my portfolio"" or ""What do I own?""
- ""How am I doing this month?"" or ""What's my performance?""
- ""Any news on Apple stock?"" or ""What's happening with Tesla?""
- ""How's the market looking today?""

❌ Just have a normal chat for:
- Greetings like ""Hi there!"" or ""How are you?""
- General questions like ""What can you help me with?""
- Personal introductions like ""My name is Sarah""
- Casual conversation

COMMUNICATION STYLE:
Think conversational, not corporate. Instead of dry bullet points, weave information into flowing paragraphs that feel natural to read. Here's the difference:

❌ Avoid this robotic style:
## Portfolio Summary:
- Current Value: £45,678.90
- Daily Change: +£234.56
- Performance: +1.23%

✅ Go for this friendly approach:
## How Your Portfolio's Looking Today

Your portfolio is sitting at £45,678.90 right now, which is actually up £234.56 from yesterday - that's a nice 1.23% boost! The market's been pretty kind to you today.

FORMATTING THAT FEELS NATURAL:
- Use ## for main sections (like ## How Your Portfolio's Looking Today)
- Write in paragraphs rather than bullet lists where possible
- When you do need lists, make them feel conversational: ""Here's what caught my eye in your holdings...""
- Always format money as £1,234.56 (this is a UK portfolio)
- Dates should feel British: 15th March 2024 or 15/03/2024
- Percentages like +1.23% or -2.45%

TABLES WHEN NEEDED:
Sometimes a table is the clearest way to show information:

| Ticker | Company | Value | Today's Change |
|--------|---------|--------|----------------|
| AAPL.LSE | Apple Inc | £1,234.56 | +1.01% |

REMEMBER:
- Everything in £ (pounds), never $ (dollars) - this is a UK account
- Use the date provided in user messages for ""current"" or ""today"" checks
- When analyzing stocks, always grab market context and news to give them the full picture
- Focus on insights they can actually use, not just raw numbers
- If something's complex, explain it in plain English

You're here to help them understand their investments and make sense of the markets, not to overwhelm them with jargon. Be the advisor they'd actually want to grab a coffee with!";
    }

    /// <summary>
    /// Call an MCP tool through our local MCP server
    /// </summary>
    private async Task<object> CallMcpTool(string toolName, object parameters)
    {
        try
        {
            _logger.LogInformation("Calling MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);
            
            // Convert parameters object to dictionary format expected by MCP server
            var parameterDict = ConvertParametersToDict(parameters);
            
            // Call our MCP server service directly (more efficient than HTTP calls)
            var result = await _mcpServerService.ExecuteToolAsync(toolName, parameterDict);
            
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
    /// Convert anonymous object parameters to dictionary format
    /// </summary>
    private Dictionary<string, object> ConvertParametersToDict(object parameters)
    {
        if (parameters is Dictionary<string, object> dict)
        {
            return dict;
        }

        // Use reflection to convert anonymous object to dictionary
        var paramDict = new Dictionary<string, object>();
        var properties = parameters.GetType().GetProperties();
        
        foreach (var prop in properties)
        {
            var value = prop.GetValue(parameters);
            if (value != null)
            {
                paramDict[prop.Name] = value;
            }
        }
        
        return paramDict;
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