using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;

namespace FtoConsulting.PortfolioManager.Api.Services.Ai;

/// <summary>
/// Implementation of MCP (Model Context Protocol) server service
/// </summary>
public class McpServerService : IMcpServerService
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;
    private readonly IMarketIntelligenceService _marketIntelligenceService;
    private readonly ILogger<McpServerService> _logger;
    private readonly Dictionary<string, Func<Dictionary<string, object>, Task<object>>> _tools;

    public McpServerService(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioAnalysisService portfolioAnalysisService,
        IMarketIntelligenceService marketIntelligenceService,
        ILogger<McpServerService> logger)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _portfolioAnalysisService = portfolioAnalysisService;
        _marketIntelligenceService = marketIntelligenceService;
        _logger = logger;
        _tools = new Dictionary<string, Func<Dictionary<string, object>, Task<object>>>();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing MCP server with portfolio tools");

            // Register portfolio tools
            _tools["get_portfolio_holdings"] = GetPortfolioHoldingsAsync;
            _tools["analyze_portfolio_performance"] = AnalyzePortfolioPerformanceAsync;
            _tools["get_market_context"] = GetMarketContextAsync;
            _tools["search_financial_news"] = SearchFinancialNewsAsync;
            _tools["get_market_sentiment"] = GetMarketSentimentAsync;
            _tools["compare_portfolio_performance"] = ComparePortfolioPerformanceAsync;

            _logger.LogInformation("MCP server initialized with {ToolCount} tools", _tools.Count);
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
        try
        {
            if (!_tools.ContainsKey(toolName))
            {
                throw new ArgumentException($"Tool '{toolName}' not found");
            }

            _logger.LogInformation("Executing MCP tool: {ToolName} with parameters: {Parameters}", 
                toolName, string.Join(", ", parameters.Keys));

            var result = await _tools[toolName](parameters);
            
            _logger.LogInformation("Successfully executed MCP tool: {ToolName}", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MCP tool: {ToolName}", toolName);
            throw;
        }
    }

    public async Task<IEnumerable<McpToolDefinition>> GetAvailableToolsAsync()
    {
        await Task.CompletedTask;

        return new[]
        {
            new McpToolDefinition(
                Name: "get_portfolio_holdings",
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
                Name: "analyze_portfolio_performance",
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
                Name: "get_market_context",
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
                Name: "search_financial_news",
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
                Name: "get_market_sentiment",
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
            ),
            new McpToolDefinition(
                Name: "compare_portfolio_performance",
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
            )
        };
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

    #region Tool Implementations

    private async Task<object> GetPortfolioHoldingsAsync(Dictionary<string, object> parameters)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var date = DateOnly.Parse(parameters["date"].ToString()!);

        var holdings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(accountId, date);
        
        return holdings.Select(h => new
        {
            Ticker = h.Instrument?.Ticker,
            InstrumentName = h.Instrument?.Name,
            UnitAmount = h.UnitAmount,
            CurrentValue = h.CurrentValue,
            BoughtValue = h.BoughtValue,
            DailyProfitLoss = h.DailyProfitLoss,
            DailyProfitLossPercentage = h.DailyProfitLossPercentage
        });
    }

    private async Task<object> AnalyzePortfolioPerformanceAsync(Dictionary<string, object> parameters)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var analysisDate = DateTime.Parse(parameters["analysisDate"].ToString()!);

        return await _portfolioAnalysisService.AnalyzePortfolioPerformanceAsync(accountId, analysisDate);
    }

    private async Task<object> GetMarketContextAsync(Dictionary<string, object> parameters)
    {
        var tickers = ((object[])parameters["tickers"]).Cast<string>();
        var date = DateTime.Parse(parameters["date"].ToString()!);

        return await _marketIntelligenceService.GetMarketContextAsync(tickers, date);
    }

    private async Task<object> SearchFinancialNewsAsync(Dictionary<string, object> parameters)
    {
        var tickers = ((object[])parameters["tickers"]).Cast<string>();
        var fromDate = DateTime.Parse(parameters["fromDate"].ToString()!);
        var toDate = DateTime.Parse(parameters["toDate"].ToString()!);

        return await _marketIntelligenceService.SearchFinancialNewsAsync(tickers, fromDate, toDate);
    }

    private async Task<object> GetMarketSentimentAsync(Dictionary<string, object> parameters)
    {
        var date = DateTime.Parse(parameters["date"].ToString()!);

        return await _marketIntelligenceService.GetMarketSentimentAsync(date);
    }

    private async Task<object> ComparePortfolioPerformanceAsync(Dictionary<string, object> parameters)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var startDate = DateTime.Parse(parameters["startDate"].ToString()!);
        var endDate = DateTime.Parse(parameters["endDate"].ToString()!);

        return await _portfolioAnalysisService.ComparePerformanceAsync(accountId, startDate, endDate);
    }

    #endregion
}