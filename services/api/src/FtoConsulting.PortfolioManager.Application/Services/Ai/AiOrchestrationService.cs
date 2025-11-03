using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
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
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;
    private readonly IMarketIntelligenceService _marketIntelligenceService;
    private readonly ILogger<AiOrchestrationService> _logger;
    private readonly AzureFoundryOptions _azureFoundryOptions;
    private readonly IMcpServerService _mcpServerService;

    public AiOrchestrationService(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioAnalysisService portfolioAnalysisService,
        IMarketIntelligenceService marketIntelligenceService,
        ILogger<AiOrchestrationService> logger,
        IOptions<AzureFoundryOptions> azureFoundryOptions,
        IMcpServerService mcpServerService)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _portfolioAnalysisService = portfolioAnalysisService;
        _marketIntelligenceService = marketIntelligenceService;
        _logger = logger;
        _azureFoundryOptions = azureFoundryOptions.Value;
        _mcpServerService = mcpServerService;
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
            var chatClient = azureOpenAIClient.GetChatClient("gpt-4o-mini");
            
            _logger.LogInformation("Created Azure OpenAI client and chat client successfully");
            
            // Create AI functions from our MCP tools for the agent
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create an AI agent with portfolio analysis instructions and MCP tools
            var agent = chatClient.CreateAIAgent(
                instructions: CreateAgentInstructions(accountId),
                tools: portfolioTools.ToList());

            // Process the query with the AI agent that has access to our MCP tools
            var chatMessages = new[]
            {
                new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nCurrent Date: {DateTime.Now:yyyy-MM-dd}")
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
            var chatClient = azureOpenAIClient.GetChatClient("gpt-4o-mini");
            
            _logger.LogInformation("Created Azure OpenAI client and chat client successfully for streaming");
            
            // Create AI functions from our MCP tools for the agent
            var portfolioTools = CreatePortfolioMcpFunctions();
            
            // Create an AI agent with portfolio analysis instructions and MCP tools
            var agent = chatClient.CreateAIAgent(
                instructions: CreateAgentInstructions(accountId),
                tools: portfolioTools.ToList());

            // Process the query with streaming using the AI agent
            var chatMessages = new[]
            {
                new ChatMessage(ChatRole.User, $"User Query: {query}\nAccount ID: {accountId}\nCurrent Date: {DateTime.Now:yyyy-MM-dd}")
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

    private async Task<string> GenerateResponseAsync(string query, string queryType, PortfolioAnalysisDto analysis, DateTime analysisDate, CancellationToken cancellationToken)
    {
        return queryType switch
        {
            "Performance" => await GeneratePerformanceResponse(query, analysis, analysisDate),
            "Holdings" => await GenerateHoldingsResponse(analysis, analysisDate),
            "Market" => await GenerateMarketResponse(analysis, analysisDate),
            "Risk" => await GenerateRiskResponse(analysis),
            "Comparison" => await GenerateComparisonResponse(analysis, analysisDate),
            _ => await GenerateGeneralResponse(analysis, analysisDate)
        };
    }

    private async Task<string> GeneratePerformanceResponse(string query, PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        var dateDisplay = analysisDate.ToString("MMMM dd, yyyy");
        var changeIcon = analysis.DayChangePercentage >= 0 ? "📈" : "📉";
        var changeColor = analysis.DayChangePercentage >= 0 ? "positive" : "negative";

        var response = $"📊 **Portfolio Performance for {dateDisplay}**\n\n";
        response += $"💰 **Total Value:** ${analysis.TotalValue:N2}\n";
        response += $"{changeIcon} **Daily Change:** ${analysis.DayChange:N2} ({analysis.DayChangePercentage:P2})\n";
        response += $"📋 **Holdings:** {analysis.HoldingPerformance.Count()} positions\n\n";

        if (analysis.Metrics.TopPerformers.Any())
        {
            response += $"🌟 **Top Performers:** {string.Join(", ", analysis.Metrics.TopPerformers)}\n";
        }

        if (analysis.Metrics.BottomPerformers.Any())
        {
            response += $"⚠️ **Underperformers:** {string.Join(", ", analysis.Metrics.BottomPerformers)}\n";
            
            // If user is asking specifically about underperformers, provide detailed analysis
            if (query.ToLower().Contains("lowest") || query.ToLower().Contains("worst") || 
                query.ToLower().Contains("underperform") || query.ToLower().Contains("poor") ||
                analysis.Metrics.BottomPerformers.Any(ticker => query.ToUpper().Contains(ticker)))
            {
                response += "\n📊 **Detailed Analysis of Underperformers:**\n\n";
                
                foreach (var ticker in analysis.Metrics.BottomPerformers)
                {
                    var holding = analysis.HoldingPerformance.FirstOrDefault(h => h.Ticker == ticker);
                    if (holding != null)
                    {
                        response += $"**{holding.InstrumentName} ({ticker})**\n";
                        response += $"💵 Current Value: ${holding.CurrentValue:N2}\n";
                        response += $"📉 Daily Change: ${holding.DayChange:N2} ({holding.DayChangePercentage:P2})\n";
                        response += $"📊 Total Return: ${holding.TotalReturn:N2} ({holding.TotalReturnPercentage:P2})\n";
                        
                        // Get detailed market context for this specific ticker
                        try
                        {
                            var tickerArray = ConvertTickersForEodCompatibility(new List<string> { ticker });
                            var marketContext = await _marketIntelligenceService.GetMarketContextAsync(tickerArray, analysisDate);
                            
                            if (marketContext.RelevantNews.Any())
                            {
                                response += $"\n📰 **Recent News & Analysis:**\n";
                                foreach (var news in marketContext.RelevantNews.Take(2))
                                {
                                    response += $"• **{news.Title}**\n";
                                    if (news.SentimentScore != 0.5m)
                                    {
                                        var sentimentEmoji = news.SentimentScore > 0.7m ? "😊" : 
                                                           news.SentimentScore < 0.3m ? "😟" : "😐";
                                        response += $"  {sentimentEmoji} Sentiment: {news.SentimentScore:N2}\n";
                                    }
                                    
                                    if (!string.IsNullOrEmpty(news.Summary) && news.Summary.Length > 50)
                                    {
                                        var excerpt = news.Summary.Length > 300 ? 
                                            news.Summary.Substring(0, 300) + "..." : 
                                            news.Summary;
                                        response += $"  {excerpt}\n";
                                    }
                                    response += "\n";
                                }
                            }
                        }
                        catch
                        {
                            response += "📊 Market analysis temporarily unavailable for this ticker.\n";
                        }
                        
                        response += "\n---\n\n";
                    }
                }
            }
        }

        response += $"🎯 **Risk Profile:** {analysis.Metrics.RiskProfile}";

        return response;
    }

    private async Task<string> GenerateHoldingsResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        var response = $"📋 **Your Portfolio Holdings - {analysisDate:MMMM dd, yyyy}**\n\n";
        
        foreach (var holding in analysis.HoldingPerformance.OrderByDescending(h => h.CurrentValue))
        {
            var changeIcon = holding.DayChangePercentage >= 0 ? "📈" : "📉";
            response += $"• **{holding.InstrumentName}** ({holding.Ticker})\n";
            response += $"  💵 Value: ${holding.CurrentValue:N2} | Units: {holding.UnitAmount:N2}\n";
            response += $"  {changeIcon} Daily: ${holding.DayChange:N2} ({holding.DayChangePercentage:P2})\n";
            response += $"  📊 Total Return: ${holding.TotalReturn:N2} ({holding.TotalReturnPercentage:P2})\n\n";
        }

        return response;
    }

    private async Task<string> GenerateMarketResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        // Get tickers for market analysis
        var portfolioTickers = analysis.HoldingPerformance.Select(h => h.Ticker).ToList();
        
        // Convert tickers to EOD-compatible format and add fallbacks
        var tickers = ConvertTickersForEodCompatibility(portfolioTickers);
        
        try
        {
            var marketContext = await _marketIntelligenceService.GetMarketContextAsync(tickers, analysisDate);
            
            var response = $"📈 **Market Context for {analysisDate:MMMM dd, yyyy}**\n\n";
            response += $"🌍 **Market Summary:** {marketContext.MarketSummary}\n\n";
            response += $"😊 **Sentiment:** {marketContext.Sentiment.SentimentLabel} (Score: {marketContext.Sentiment.OverallSentimentScore:N2})\n";
            response += $"📊 **Fear & Greed Index:** {marketContext.Sentiment.FearGreedIndex}\n\n";

            if (marketContext.RelevantNews.Any())
            {
                response += "📰 **Recent News & Analysis:**\n\n";
                foreach (var news in marketContext.RelevantNews.Take(5))
                {
                    response += $"**{news.Title}**\n";
                    response += $"*Source: {news.Source} | {news.PublishedDate:MMM dd, yyyy}*\n";
                    
                    // Show sentiment if available
                    if (news.SentimentScore != 0.5m) // Not neutral
                    {
                        var sentimentEmoji = news.SentimentScore > 0.7m ? "😊" : 
                                           news.SentimentScore < 0.3m ? "😟" : "😐";
                        response += $"{sentimentEmoji} Sentiment: {news.SentimentScore:N2}\n";
                    }
                    
                    // Show key excerpt from content
                    if (!string.IsNullOrEmpty(news.Summary))
                    {
                        var excerpt = news.Summary.Length > 400 ? 
                            news.Summary.Substring(0, 400) + "..." : 
                            news.Summary;
                        response += $"{excerpt}\n";
                    }
                    
                    // Show related tickers if any
                    if (news.RelatedTickers.Any())
                    {
                        response += $"🎯 Related: {string.Join(", ", news.RelatedTickers.Take(3))}\n";
                    }
                    
                    if (!string.IsNullOrEmpty(news.Url))
                    {
                        response += $"🔗 [Read more]({news.Url})\n";
                    }
                    
                    response += "\n---\n\n";
                }
                
                // Remove the last separator
                if (response.EndsWith("---\n\n"))
                {
                    response = response.Substring(0, response.Length - 5);
                }
            }

            return response;
        }
        catch
        {
            return "📈 Market analysis is temporarily unavailable. Please try again later.";
        }
    }

    private async Task<string> GenerateRiskResponse(PortfolioAnalysisDto analysis)
    {
        await Task.CompletedTask;

        var response = $"⚖️ **Portfolio Risk Analysis**\n\n";
        response += $"🎯 **Risk Profile:** {analysis.Metrics.RiskProfile}\n";
        response += $"📊 **Daily Volatility:** {analysis.Metrics.DailyVolatility:P2}\n\n";

        // Concentration analysis
        var totalValue = analysis.TotalValue;
        var concentrationRisks = analysis.HoldingPerformance
            .Where(h => h.CurrentValue / totalValue > 0.25m)
            .ToList();

        if (concentrationRisks.Any())
        {
            response += "⚠️ **Concentration Risks:**\n";
            foreach (var risk in concentrationRisks)
            {
                var percentage = (risk.CurrentValue / totalValue) * 100;
                response += $"• {risk.Ticker}: {percentage:N1}% of portfolio\n";
            }
            response += "\n💡 Consider diversifying positions above 25% allocation.\n";
        }
        else
        {
            response += "✅ **Good diversification** - No single position exceeds 25% allocation.\n";
        }

        return response;
    }

    private async Task<string> GenerateComparisonResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        try
        {
            var previousDate = analysisDate.AddDays(-1);
            var comparison = await _portfolioAnalysisService.ComparePerformanceAsync(
                analysis.AccountId, previousDate, analysisDate);

            var response = $"📊 **Portfolio Comparison: {previousDate:MMM dd} vs {analysisDate:MMM dd}**\n\n";
            response += $"💰 **Value Change:** ${comparison.StartValue:N2} → ${comparison.EndValue:N2}\n";
            response += $"📈 **Net Change:** ${comparison.TotalChange:N2} ({comparison.TotalChangePercentage:P2})\n\n";
            response += $"📋 **Trend:** {comparison.Insights.OverallTrend}\n";

            if (comparison.Insights.KeyDrivers.Any())
            {
                response += $"\n🔑 **Key Drivers:**\n";
                foreach (var driver in comparison.Insights.KeyDrivers.Take(3))
                {
                    response += $"• {driver}\n";
                }
            }

            return response;
        }
        catch
        {
            return "📊 Portfolio comparison is not available for the requested dates.";
        }
    }

    private async Task<string> GenerateGeneralResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        // Default to performance response for general queries
        return await GeneratePerformanceResponse("general", analysis, analysisDate);
    }

    private IEnumerable<InsightDto> GenerateInsights(PortfolioAnalysisDto analysis)
    {
        var insights = new List<InsightDto>();

        // Performance insights
        if (analysis.DayChangePercentage > 0.02m) // > 2%
        {
            insights.Add(new InsightDto(
                Type: "Performance",
                Title: "Strong Daily Performance",
                Description: $"Your portfolio gained {analysis.DayChangePercentage:P2} today, outperforming typical market movements.",
                Severity: "Positive"
            ));
        }
        else if (analysis.DayChangePercentage < -0.02m) // < -2%
        {
            insights.Add(new InsightDto(
                Type: "Performance", 
                Title: "Notable Daily Decline",
                Description: $"Your portfolio declined {Math.Abs(analysis.DayChangePercentage):P2} today. Consider reviewing individual holdings.",
                Severity: "Warning"
            ));
        }

        // Concentration insights
        var topHolding = analysis.HoldingPerformance.OrderByDescending(h => h.CurrentValue).FirstOrDefault();
        if (topHolding != null && (topHolding.CurrentValue / analysis.TotalValue) > 0.3m)
        {
            insights.Add(new InsightDto(
                Type: "Risk",
                Title: "High Concentration Risk",
                Description: $"{topHolding.Ticker} represents a large portion of your portfolio. Consider diversification.",
                Severity: "Warning",
                RelatedTickers: new[] { topHolding.Ticker }
            ));
        }

        return insights;
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
        return $@"You are a financial portfolio analyst for Account ID {accountId}.

CRITICAL: Always format responses using proper markdown syntax:
- Use ## for section headers (## Market Analysis:)
- Use - for bullet points with content on the same line
- Never put empty bullet points on separate lines
- Format: - **Item:** Description here (all on one line)

Example format:
## Recent News:
- **Article:** Market volatility affects sector
- **Date:** 2025-11-03
- **Impact:** Significant price movements observed

Your tools: portfolio analysis, market data, financial insights.
Always use current date {DateTime.Now:yyyy-MM-dd} unless specified.
Focus on actionable insights with proper UK currency formatting (£).

When analyzing instruments, always use GetMarketContext to retrieve detailed news and analysis.
Present financial information clearly and provide actionable insights.

When users ask about their portfolio, use the appropriate tools to get real data rather than making assumptions.
- Format currency as £1,234.56 (with commas for thousands and 2 decimal places)
- Use UK date format where appropriate (DD/MM/YYYY or DD MMM YYYY)
- Percentages should be formatted as +1.23% or -1.23%
EXAMPLE TABLE FORMAT:
| Ticker | Name | Value | Change | Change % |
|--------|------|-------|--------|----------|
| AAPL.LSE | Apple Inc | £1,234.56 | +£12.34 | +1.01% |

IMPORTANT: Never use $ (USD) symbols - this is a UK portfolio and all values should be in £ (GBP).

When users ask about their portfolio, use the appropriate tools to get real data rather than making assumptions.";
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
    /// Convert portfolio tickers to EOD-compatible format with fallbacks
    /// </summary>
    private List<string> ConvertTickersForEodCompatibility(List<string> portfolioTickers)
    {
        var validTickers = portfolioTickers
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t.Trim().ToUpperInvariant())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        // If we have no tickers, provide some default market representatives
        if (!validTickers.Any())
        {
            return new List<string> { "AAPL.US", "MSFT.US", "GOOGL.US", "BP.LSE", "HSBA.LSE", "AZN.LSE" };
        }

        return validTickers;
    }

    /// <summary>
    /// Convert a single ticker to EOD format (removed - using tickers as-is)
    /// </summary>
    private string ConvertSingleTickerToEodFormat(string ticker)
    {
        // Simply return the ticker as-is after basic cleanup
        return string.IsNullOrEmpty(ticker) ? string.Empty : ticker.Trim().ToUpperInvariant();
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

    private string FixBasicFormatting(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        // Replace bullet symbols with dashes
        var cleaned = response
            .Replace("• ", "- ")
            .Replace("◦ ", "- ")
            .Replace("▪ ", "- ");

        // Simple regex to fix the most common pattern: standalone bullet + header on next line
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^•\s*\n([A-Za-z][^:\n]*:)\s*$",
            "## $1",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        // Fix standalone dashes followed by headers
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^-\s*\n([A-Za-z][^:\n]*:)\s*$",
            "## $1",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        return cleaned;
    }
}