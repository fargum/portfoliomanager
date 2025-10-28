using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Api.Services.Ai.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Azure.AI.OpenAI;
using Azure;
using System.ComponentModel;

namespace FtoConsulting.PortfolioManager.Api.Services.Ai;

/// <summary>
/// Implementation of MCP (Model Context Protocol) server service using Microsoft Agent Framework
/// This service bridges between our custom MCP controller and the Microsoft Agent Framework MCP server
/// </summary>
public class McpServerService : IMcpServerService
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;
    private readonly IMarketIntelligenceService _marketIntelligenceService;
    private readonly ILogger<McpServerService> _logger;
    private readonly AzureFoundryOptions _azureFoundryOptions;
    
    // Direct tool references for execution
    private readonly PortfolioHoldingsTool _portfolioHoldingsTool;
    private readonly PortfolioAnalysisTool _portfolioAnalysisTool;
    private readonly PortfolioComparisonTool _portfolioComparisonTool;
    private readonly MarketIntelligenceTool _marketIntelligenceTool;

    public McpServerService(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioAnalysisService portfolioAnalysisService,
        IMarketIntelligenceService marketIntelligenceService,
        ILogger<McpServerService> logger,
        IOptions<AzureFoundryOptions> azureFoundryOptions,
        PortfolioHoldingsTool portfolioHoldingsTool,
        PortfolioAnalysisTool portfolioAnalysisTool,
        PortfolioComparisonTool portfolioComparisonTool,
        MarketIntelligenceTool marketIntelligenceTool)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _portfolioAnalysisService = portfolioAnalysisService;
        _marketIntelligenceService = marketIntelligenceService;
        _logger = logger;
        _azureFoundryOptions = azureFoundryOptions.Value;
        _portfolioHoldingsTool = portfolioHoldingsTool;
        _portfolioAnalysisTool = portfolioAnalysisTool;
        _portfolioComparisonTool = portfolioComparisonTool;
        _marketIntelligenceTool = marketIntelligenceTool;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing MCP server with portfolio tools using Microsoft Agent Framework");

            // For now, create a simpler implementation that works with the current packages
            // TODO: Enhance with proper Agent Framework integration when APIs are stable

            _logger.LogInformation("MCP server initialized successfully");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MCP server");
            throw;
        }
    }

    public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);
        
        try
        {
            return toolName switch
            {
                "GetPortfolioHoldings" => await ExecuteGetPortfolioHoldings(parameters, cancellationToken),
                "AnalyzePortfolioPerformance" => await ExecuteAnalyzePortfolioPerformance(parameters, cancellationToken),
                "ComparePortfolioPerformance" => await ExecuteComparePortfolioPerformance(parameters, cancellationToken),
                "GetMarketContext" => await ExecuteGetMarketContext(parameters, cancellationToken),
                "SearchFinancialNews" => await ExecuteSearchFinancialNews(parameters, cancellationToken),
                "GetMarketSentiment" => await ExecuteGetMarketSentiment(parameters, cancellationToken),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            throw;
        }
    }

    public Task<IEnumerable<McpToolDefinition>> GetAvailableToolsAsync()
    {
        _logger.LogInformation("Returning available MCP tools from integrated Microsoft Agent Framework");
        
        var tools = new[]
        {
            new McpToolDefinition(
                Name: "GetPortfolioHoldings",
                Description: "Retrieve portfolio holdings for a specific account and date",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["accountId"] = new { type = "integer", description = "Account ID" },
                        ["date"] = new { type = "string", description = "Date in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "accountId", "date" }
                }
            ),
            new McpToolDefinition(
                Name: "AnalyzePortfolioPerformance",
                Description: "Analyze portfolio performance and generate insights for a specific date",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["accountId"] = new { type = "integer", description = "Account ID" },
                        ["analysisDate"] = new { type = "string", description = "Analysis date in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "accountId", "analysisDate" }
                }
            ),
            new McpToolDefinition(
                Name: "ComparePortfolioPerformance",
                Description: "Compare portfolio performance between two dates",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["accountId"] = new { type = "integer", description = "Account ID" },
                        ["startDate"] = new { type = "string", description = "Start date in YYYY-MM-DD format" },
                        ["endDate"] = new { type = "string", description = "End date in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "accountId", "startDate", "endDate" }
                }
            ),
            new McpToolDefinition(
                Name: "GetMarketContext",
                Description: "Get market context and news for specific stock tickers",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["tickers"] = new { type = "array", items = new { type = "string" }, description = "List of stock tickers" },
                        ["date"] = new { type = "string", description = "Date for market analysis in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "tickers", "date" }
                }
            ),
            new McpToolDefinition(
                Name: "SearchFinancialNews",
                Description: "Search for financial news related to specific tickers within a date range",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["tickers"] = new { type = "array", items = new { type = "string" }, description = "List of stock tickers" },
                        ["fromDate"] = new { type = "string", description = "Start date in YYYY-MM-DD format" },
                        ["toDate"] = new { type = "string", description = "End date in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "tickers", "fromDate", "toDate" }
                }
            ),
            new McpToolDefinition(
                Name: "GetMarketSentiment",
                Description: "Get overall market sentiment and indicators for a specific date",
                Schema: new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["date"] = new { type = "string", description = "Date for sentiment analysis in YYYY-MM-DD format" }
                    },
                    ["required"] = new[] { "date" }
                }
            )
        };

        return Task.FromResult<IEnumerable<McpToolDefinition>>(tools);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if core services are available
            var testDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(1, testDate, CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #region Tool Execution Methods

    private async Task<object> ExecuteGetPortfolioHoldings(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var date = parameters["date"].ToString()!;
        
        return await _portfolioHoldingsTool.GetPortfolioHoldings(accountId, date, cancellationToken);
    }

    private async Task<object> ExecuteAnalyzePortfolioPerformance(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var analysisDate = parameters["analysisDate"].ToString()!;
        
        return await _portfolioAnalysisTool.AnalyzePortfolioPerformance(accountId, analysisDate, cancellationToken);
    }

    private async Task<object> ExecuteComparePortfolioPerformance(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var startDate = parameters["startDate"].ToString()!;
        var endDate = parameters["endDate"].ToString()!;
        
        return await _portfolioComparisonTool.ComparePortfolioPerformance(accountId, startDate, endDate, cancellationToken);
    }

    private async Task<object> ExecuteGetMarketContext(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ((IEnumerable<object>)parameters["tickers"]).Select(t => t.ToString()!).ToArray();
        var date = parameters["date"].ToString()!;
        
        return await _marketIntelligenceTool.GetMarketContext(tickers, date, cancellationToken);
    }

    private async Task<object> ExecuteSearchFinancialNews(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ((IEnumerable<object>)parameters["tickers"]).Select(t => t.ToString()!).ToArray();
        var fromDate = parameters["fromDate"].ToString()!;
        var toDate = parameters["toDate"].ToString()!;
        
        return await _marketIntelligenceTool.SearchFinancialNews(tickers, fromDate, toDate, cancellationToken);
    }

    private async Task<object> ExecuteGetMarketSentiment(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var date = parameters["date"].ToString()!;
        
        return await _marketIntelligenceTool.GetMarketSentiment(date, cancellationToken);
    }

    #endregion
}