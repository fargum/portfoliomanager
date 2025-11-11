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
using System.Text.Json;

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
        _logger.LogInformation("Returning available MCP tools from centralized tool registry");
        
        var tools = PortfolioToolRegistry.GetMcpToolDefinitions();
        return Task.FromResult(tools);
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
        var tickers = ExtractStringArrayFromJsonParameter(parameters["tickers"]);
        var date = parameters["date"].ToString()!;
        
        return await _marketIntelligenceTool.GetMarketContext(tickers, date, cancellationToken);
    }

    private async Task<object> ExecuteSearchFinancialNews(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ExtractStringArrayFromJsonParameter(parameters["tickers"]);
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

            // Validate response content before parsing
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogError("EOD MCP server returned empty response for tool {ToolName}", toolName);
                throw new InvalidOperationException($"EOD MCP server returned empty response for tool '{toolName}'");
            }

            // Check for common error indicators
            if (responseContent.StartsWith("Error:") || responseContent.StartsWith("ERROR:") ||
                responseContent.StartsWith("<!DOCTYPE") || responseContent.StartsWith("<html"))
            {
                _logger.LogError("EOD MCP server returned error response for tool {ToolName}: {Error}", toolName, responseContent);
                throw new InvalidOperationException($"EOD MCP server returned error for tool '{toolName}': {responseContent}");
            }

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
                        
                        // Validate JSON data before parsing
                        if (string.IsNullOrWhiteSpace(jsonData) || !IsValidJsonStart(jsonData))
                        {
                            var analysis = string.IsNullOrWhiteSpace(jsonData) ? "JSON data is null or empty" : AnalyzeInvalidContent(jsonData);
                            _logger.LogError("Invalid JSON data in SSE response for tool {ToolName}. Analysis: {Analysis}", toolName, analysis);
                            throw new InvalidOperationException($"Invalid JSON in SSE response for tool '{toolName}': {analysis}");
                        }
                        
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
                    // Validate JSON format before parsing
                    if (!IsValidJsonStart(responseContent))
                    {
                        var analysis = AnalyzeInvalidContent(responseContent);
                        _logger.LogError("Response is not valid JSON for tool {ToolName}. Analysis: {Analysis}", toolName, analysis);
                        throw new InvalidOperationException($"EOD MCP server returned non-JSON response for tool '{toolName}': {analysis}");
                    }

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
                _logger.LogError(ex, "Failed to parse EOD MCP response as JSON for tool {ToolName}. Response content: {Response}", toolName, responseContent);
                throw new InvalidOperationException($"EOD MCP server returned invalid JSON response for tool '{toolName}': {ex.Message}. Response was: {responseContent}", ex);
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

    /// <summary>
    /// Helper method to validate if a string starts with valid JSON characters
    /// </summary>
    /// <param name="content">The content to validate</param>
    /// <returns>True if the content appears to start with valid JSON</returns>
    private static bool IsValidJsonStart(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var trimmed = content.TrimStart();
        return trimmed.StartsWith("{") || trimmed.StartsWith("[") || trimmed.StartsWith("\"") ||
               trimmed.StartsWith("true", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("false", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("null", StringComparison.OrdinalIgnoreCase) ||
               char.IsDigit(trimmed[0]) || trimmed[0] == '-';
    }

    /// <summary>
    /// Helper method to provide more detailed analysis of invalid response content
    /// </summary>
    /// <param name="content">The response content to analyze</param>
    /// <returns>Diagnostic information about the content</returns>
    private static string AnalyzeInvalidContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "Content is null or empty";

        var trimmed = content.Trim();
        var firstChars = trimmed.Length > 50 ? trimmed.Substring(0, 50) + "..." : trimmed;
        
        if (trimmed.StartsWith(":"))
            return $"Content starts with colon (:) - possibly malformed MCP response. First 50 chars: '{firstChars}'";
        
        if (trimmed.StartsWith("Error:") || trimmed.StartsWith("ERROR:"))
            return $"Content appears to be an error message: '{firstChars}'";
        
        if (trimmed.StartsWith("<!DOCTYPE") || trimmed.StartsWith("<html"))
            return $"Content appears to be HTML: '{firstChars}'";
        
        if (trimmed.Contains("Internal Server Error"))
            return $"Content indicates server error: '{firstChars}'";
        
        return $"Content format unrecognized. First 50 chars: '{firstChars}'";
    }

    /// <summary>
    /// Helper method to extract string array from JSON parameter that might be JsonElement
    /// </summary>
    /// <param name="parameter">The parameter value (either JsonElement array or IEnumerable)</param>
    /// <returns>Array of strings extracted from the parameter</returns>
    private static string[] ExtractStringArrayFromJsonParameter(object parameter)
    {
        if (parameter is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
            }
            else if (jsonElement.ValueKind == JsonValueKind.String)
            {
                // Single string value
                var value = jsonElement.GetString();
                return !string.IsNullOrEmpty(value) ? new[] { value } : Array.Empty<string>();
            }
        }
        else if (parameter is IEnumerable<object> enumerable)
        {
            // Legacy handling for non-JsonElement arrays
            return enumerable.Select(item => item?.ToString()!)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
        else if (parameter is string singleString)
        {
            // Single string parameter
            return !string.IsNullOrEmpty(singleString) ? new[] { singleString } : Array.Empty<string>();
        }

        return Array.Empty<string>();
    }

    #endregion
}