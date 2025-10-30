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
using System.Collections.Concurrent;
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
    
    // Static session storage to persist across service instances (EOD requires session persistence)
    private static readonly ConcurrentDictionary<string, string> _eodSessionCache = new();
    private static readonly SemaphoreSlim _sessionSemaphore = new(1, 1);
    
    /// <summary>
    /// Get the session ID for the EOD MCP server
    /// </summary>
    private string? GetEodSessionId() => _eodSessionCache.TryGetValue(_eodApiOptions.McpServerUrl, out var sessionId) ? sessionId : null;
    
    /// <summary>
    /// Set the session ID for the EOD MCP server
    /// </summary>
    private void SetEodSessionId(string sessionId) => _eodSessionCache[_eodApiOptions.McpServerUrl] = sessionId;

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
                return await CallEodMcpServerAsync(toolName, parameters, cancellationToken);
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
    /// Make HTTP request to EOD Historical Data MCP server with proper session management
    /// </summary>
    private async Task<object> CallEodMcpServerAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate EOD API token is configured
        if (string.IsNullOrEmpty(_eodApiOptions.Token))
        {
            _logger.LogError("EOD API token not configured - cannot call MCP server");
            throw new InvalidOperationException("Cannot call EOD MCP server: API token not configured");
        }

        // Ensure we have a valid session
        await EnsureEodSessionAsync(cancellationToken);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            // Build the EOD MCP server URL with API key parameter
            var eodServerUrl = $"{_eodApiOptions.McpServerUrl}?apikey={_eodApiOptions.Token}";
            
            // Add required headers as per EOD support instructions
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
            
            // Add session ID header as specified by EOD support
            var sessionId = GetEodSessionId();
            if (!string.IsNullOrEmpty(sessionId))
            {
                httpClient.DefaultRequestHeaders.Add("Mcp-Session-Id", sessionId);
                _logger.LogInformation("Added EOD session ID header: {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("No session ID available for EOD MCP request");
            }

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

            _logger.LogInformation("Calling EOD MCP server: {Url} with tool {ToolName}", 
                eodServerUrl, toolName);

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

            // Parse MCP response - handle both SSE and JSON formats
            try
            {
                // Check if the response is Server-Sent Events format
                if (responseContent.StartsWith("event: message"))
                {
                    // Extract JSON from SSE format
                    var lines = responseContent.Split('\n');
                    var dataLine = lines.FirstOrDefault(line => line.StartsWith("data: "));
                    if (dataLine != null)
                    {
                        var jsonData = dataLine.Substring("data: ".Length);
                        var mcpResponse = System.Text.Json.JsonSerializer.Deserialize<object>(jsonData);
                        if (mcpResponse == null)
                        {
                            _logger.LogError("Failed to deserialize SSE JSON data for tool {ToolName}", toolName);
                            throw new InvalidOperationException($"Invalid JSON in SSE response for tool '{toolName}'");
                        }
                        return mcpResponse;
                    }
                    else
                    {
                        _logger.LogError("No data line found in SSE response for tool {ToolName}", toolName);
                        throw new InvalidOperationException($"Invalid SSE response format for tool '{toolName}'");
                    }
                }
                else
                {
                    // Try to parse as regular JSON
                    var mcpResponse = System.Text.Json.JsonSerializer.Deserialize<object>(responseContent);
                    
                    if (mcpResponse == null)
                    {
                        _logger.LogError("EOD MCP server returned null response for tool {ToolName}", toolName);
                        throw new InvalidOperationException($"EOD MCP server returned invalid response for tool '{toolName}'");
                    }
                    
                    return mcpResponse;
                }
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

    /// <summary>
    /// Ensure we have a valid EOD session ID by sending InitializeRequest as per EOD support instructions
    /// </summary>
    private async Task EnsureEodSessionAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(GetEodSessionId()))
        {
            return; // Session already exists
        }

        await _sessionSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(GetEodSessionId()))
            {
                return;
            }

            _logger.LogInformation("Initializing EOD MCP session with InitializeRequest");

            // Validate EOD API token is configured
            if (string.IsNullOrEmpty(_eodApiOptions.Token))
            {
                _logger.LogError("EOD API token not configured - cannot initialize session");
                throw new InvalidOperationException("Cannot initialize EOD MCP session: API token not configured");
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            // Build the EOD MCP server URL with API key parameter
            var eodServerUrl = $"{_eodApiOptions.McpServerUrl}?apikey={_eodApiOptions.Token}";
            
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

            // Send InitializeRequest as per EOD support instructions
            var initRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    clientInfo = new
                    {
                        name = "PortfolioManager",
                        version = "1.0"
                    }
                }
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(initRequest);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending InitializeRequest to EOD MCP server");

            var response = await httpClient.PostAsync(eodServerUrl, httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to initialize EOD MCP session: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to initialize EOD MCP session: {response.StatusCode} - {errorContent}");
            }

            // Extract session ID from response header as per EOD support instructions
            // Try both header name variations (Mcp-Session-Id and mcp-session-id)
            var sessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIdValues) 
                ? sessionIdValues.FirstOrDefault()
                : response.Headers.TryGetValues("mcp-session-id", out var lowerSessionIdValues)
                    ? lowerSessionIdValues.FirstOrDefault()
                    : null;

            if (!string.IsNullOrEmpty(sessionId))
            {
                SetEodSessionId(sessionId);
                _logger.LogInformation("Successfully initialized EOD MCP session with ID: {SessionId}", sessionId);
                return;
            }

            // If no session ID in header, log error
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("EOD MCP server did not return Mcp-Session-Id header. Response: {Response}", responseContent);
            throw new InvalidOperationException("EOD MCP server did not return required Mcp-Session-Id header in InitializeRequest response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EOD MCP session");
            throw new InvalidOperationException($"Failed to initialize EOD MCP session: {ex.Message}", ex);
        }
        finally
        {
            _sessionSemaphore.Release();
        }
    }

    #endregion
}