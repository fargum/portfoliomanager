using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
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
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of MCP (Model Context Protocol) server service using Microsoft Agent Framework
/// This service bridges between our custom MCP controller and the Microsoft Agent Framework MCP server
/// </summary>
public class McpServerService : IMcpServerService
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;
    private readonly ILogger<McpServerService> _logger;
    private readonly AzureFoundryOptions _azureFoundryOptions;
    private readonly EodApiOptions _eodApiOptions;
    
    // Direct tool references for execution
    private readonly PortfolioHoldingsTool _portfolioHoldingsTool;
    private readonly PortfolioAnalysisTool _portfolioAnalysisTool;
    private readonly PortfolioComparisonTool _portfolioComparisonTool;
    private readonly MarketIntelligenceTool _marketIntelligenceTool;

    public McpServerService(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioAnalysisService portfolioAnalysisService,
        ILogger<McpServerService> logger,
        IOptions<AzureFoundryOptions> azureFoundryOptions,
        IOptions<EodApiOptions> eodApiOptions,
        PortfolioHoldingsTool portfolioHoldingsTool,
        PortfolioAnalysisTool portfolioAnalysisTool,
        PortfolioComparisonTool portfolioComparisonTool,
        MarketIntelligenceTool marketIntelligenceTool)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _portfolioAnalysisService = portfolioAnalysisService;
        _logger = logger;
        _azureFoundryOptions = azureFoundryOptions.Value;
        _eodApiOptions = eodApiOptions.Value;
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

    /// <summary>
    /// Call a tool on an external MCP server (like EOD Historical Data)
    /// </summary>
    /// <param name="serverId">External MCP server identifier</param>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="parameters">Tool parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    public async Task<object> CallMcpToolAsync(string serverId, string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling external MCP tool {ToolName} on server {ServerId}", toolName, serverId);

            // Handle EOD Historical Data MCP server calls
            if (serverId == _eodApiOptions.McpServerUrl)
            {
                // For now, return a graceful message indicating the service is being configured
                _logger.LogWarning("EOD MCP server integration is currently being configured - returning graceful fallback");
                return new { 
                    message = "EOD Historical Data service is currently being configured. Please check back later.",
                    status = "service_unavailable",
                    timestamp = DateTime.UtcNow
                };
            }

            throw new NotSupportedException($"MCP server '{serverId}' is not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling external MCP tool {ToolName} on server {ServerId}", toolName, serverId);
            throw;
        }
    }

    /// <summary>
    /// Make HTTP request to EOD Historical Data MCP server
    /// </summary>
    private async Task<object> CallEodMcpServerAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate EOD API token is configured
        if (string.IsNullOrEmpty(_eodApiOptions.Token))
        {
            _logger.LogError("EOD API token not configured - cannot call MCP server");
            throw new InvalidOperationException("Cannot call EOD MCP server: API token not configured");
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            // Build the EOD MCP server URL with API key parameter as required by their documentation
            var eodServerUrl = $"{_eodApiOptions.McpServerUrl}?apikey={_eodApiOptions.Token}";
            
            // Add Accept headers as required by EOD MCP server
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

            // Prepare MCP request payload in proper JSON-RPC 2.0 format
            var mcpRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = parameters
                }
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(mcpRequest);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling EOD MCP server: {Url} with tool {ToolName}, request: {Request}", 
                eodServerUrl, toolName, jsonContent);

            var response = await httpClient.PostAsync(eodServerUrl, httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("EOD MCP server returned {StatusCode} for tool {ToolName}: {Error}", 
                    response.StatusCode, toolName, errorContent);
                throw new InvalidOperationException($"EOD MCP server call failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("EOD MCP server response for tool {ToolName}: {Response}", toolName, responseContent);

            // Check if response is Server-Sent Events format
            if (responseContent.TrimStart().StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("EOD MCP server returned SSE response for tool {ToolName}, parsing data", toolName);
                
                // Parse SSE format to extract actual data
                var lines = responseContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                string? dataContent = null;
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        dataContent = line.Substring(5).Trim(); // Remove "data:" prefix
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(dataContent))
                {
                    _logger.LogWarning("No data found in SSE response for tool {ToolName}", toolName);
                    throw new InvalidOperationException($"No data found in SSE response for tool '{toolName}'");
                }
                
                // Try to parse the data content as JSON
                try
                {
                    var sseDataResponse = System.Text.Json.JsonSerializer.Deserialize<object>(dataContent);
                    return sseDataResponse ?? throw new InvalidOperationException($"Deserialized null response for tool '{toolName}'");
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse SSE data content as JSON for tool {ToolName}: {Data}", toolName, dataContent);
                    return dataContent; // Return raw data if not JSON
                }
            }

            // Handle regular JSON responses
            try
            {
                // Parse MCP response
                var mcpResponse = System.Text.Json.JsonSerializer.Deserialize<object>(responseContent);
                
                if (mcpResponse == null)
                {
                    _logger.LogError("EOD MCP server returned null response for tool {ToolName}", toolName);
                    throw new InvalidOperationException($"EOD MCP server returned invalid response for tool '{toolName}'");
                }
                
                return mcpResponse;
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse EOD MCP response as JSON for tool {ToolName}: {Response}", toolName, responseContent);
                throw new InvalidOperationException($"EOD MCP server returned invalid JSON response for tool '{toolName}': {ex.Message}", ex);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling EOD MCP server for tool {ToolName}", toolName);
            throw new InvalidOperationException($"Failed to call EOD MCP server for tool '{toolName}': {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling EOD MCP server for tool {ToolName}", toolName);
            throw new InvalidOperationException($"Timeout calling EOD MCP server for tool '{toolName}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling EOD MCP server for tool {ToolName}", toolName);
            throw new InvalidOperationException($"Failed to call EOD MCP server for tool '{toolName}': {ex.Message}", ex);
        }
    }

    #endregion
}