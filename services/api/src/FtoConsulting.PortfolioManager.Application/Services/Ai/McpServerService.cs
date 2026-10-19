using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of custom MCP-style server service for portfolio tools
/// This service provides tool execution for the in-app AI tools
/// </summary>
public class McpServerService(
    IHoldingService holdingService,
    ILogger<McpServerService> logger,
    IOptions<AzureFoundryOptions> azureFoundryOptions,
    PortfolioHoldingsTool portfolioHoldingsTool,
    PortfolioAnalysisTool portfolioAnalysisTool,
    PortfolioComparisonTool portfolioComparisonTool,
    MarketIntelligenceTool marketIntelligenceTool,
    EodMarketDataTool eodMarketDataTool,
    TavilySearchTool tavilySearchTool) : IMcpServerService
{

    private readonly AzureFoundryOptions _azureFoundryOptions = azureFoundryOptions.Value;

    // Service initialization components
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private bool _isInitialized = false;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            logger.LogInformation("Initializing custom MCP server service with portfolio tools");

            // Step 1: Validate configuration
            await ValidateConfigurationAsync();
            
            // Step 2: Validate tool registry is populated
            await ValidateToolRegistryAsync(cancellationToken);

            // Step 3: Validate core service connectivity
            await ValidateServiceConnectivityAsync();

            _isInitialized = true;
            logger.LogInformation("Custom MCP server service initialization completed successfully. Available tools: {ToolCount}", 
                PortfolioToolRegistry.Tools.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing custom MCP server service");
            throw;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    #region Initialization Helper Methods

    /// <summary>
    /// Validate all configuration options are properly set
    /// </summary>
    private Task ValidateConfigurationAsync()
    {
        logger.LogDebug("Validating MCP server configuration");

        // Validate Azure Foundry configuration
        if (_azureFoundryOptions == null)
        {
            throw new InvalidOperationException("Azure Foundry options not configured");
        }

        logger.LogDebug("MCP server configuration validation completed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate that portfolio tools are properly registered
    /// </summary>
    private async Task ValidateToolRegistryAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Validating portfolio tool registry");

        var portfolioTools = PortfolioToolRegistry.GetMcpToolDefinitions();
        
        if (!portfolioTools.Any())
        {
            throw new InvalidOperationException("No portfolio tools found in registry");
        }
        
        logger.LogDebug("Portfolio tool registry validated. Available tools: {ToolNames}", 
            string.Join(", ", portfolioTools.Select(t => t.Name)));
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Validate connectivity to core services
    /// </summary>
    private async Task ValidateServiceConnectivityAsync()
    {
        logger.LogDebug("Validating core service connectivity");

        try
        {
            // Test basic service connectivity by checking if we can access holding service
            var testDate = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // This will test database connectivity and core service availability
            // Using a likely non-existent account ID to avoid returning large datasets
            await holdingService.GetHoldingsByAccountAndDateAsync(int.MaxValue, testDate, CancellationToken.None);
            
            logger.LogDebug("Core service connectivity validation completed");
        }
        catch (Exception ex)
        {
            // Log the validation attempt but don't fail initialization
            // Services might be temporarily unavailable during startup
            logger.LogWarning(ex, "Core service connectivity validation failed - services may be starting up");
        }
    }

    #endregion

    public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Executing MCP tool: {ToolName} with parameters: {@Parameters}", toolName, parameters);
        
        try
        {
            return toolName switch
            {
                "GetPortfolioHoldings" => await ExecuteGetPortfolioHoldings(parameters, cancellationToken),
                "GetHoldingByTicker" => await ExecuteGetPortfolioHoldings(parameters, cancellationToken),
                "AnalyzePortfolioPerformance" => await ExecuteAnalyzePortfolioPerformance(parameters, cancellationToken),
                "ComparePortfolioPerformance" => await ExecuteComparePortfolioPerformance(parameters, cancellationToken),
                "GetMarketContext" => await ExecuteGetMarketContext(parameters, cancellationToken),
                "GetMarketSentiment" => await ExecuteGetMarketSentiment(parameters, cancellationToken),
                "GetRealTimePrices" => await ExecuteGetRealTimePrices(parameters, cancellationToken),
                "SearchRecentNews" => await ExecuteSearchRecentNews(parameters, cancellationToken),
                "ResearchCompanyFundamentals" => await ExecuteResearchCompanyFundamentals(parameters, cancellationToken),
                "GetCompanyOverview" => await ExecuteGetCompanyOverview(parameters, cancellationToken),
                "GetMarketOverview" => await ExecuteGetMarketOverview(parameters, cancellationToken),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            throw;
        }
    }

    public Task<IEnumerable<McpToolDefinition>> GetAvailableToolsAsync()
    {
        logger.LogInformation("Returning available MCP tools from centralized tool registry");
        
        var tools = PortfolioToolRegistry.GetMcpToolDefinitions();
        return Task.FromResult(tools);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if core services are available
            var testDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await holdingService.GetHoldingsByAccountAndDateAsync(1, testDate, CancellationToken.None);
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
        var ticker = parameters.TryGetValue("ticker", out var tickerVal) ? tickerVal?.ToString() : null;
        
        return await portfolioHoldingsTool.GetPortfolioHoldings(accountId, date, ticker, cancellationToken);
    }

    private async Task<object> ExecuteAnalyzePortfolioPerformance(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var analysisDate = parameters["analysisDate"].ToString()!;
        
        return await portfolioAnalysisTool.AnalyzePortfolioPerformance(accountId, analysisDate, cancellationToken);
    }

    private async Task<object> ExecuteComparePortfolioPerformance(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var accountId = Convert.ToInt32(parameters["accountId"]);
        var startDate = parameters["startDate"].ToString()!;
        var endDate = parameters["endDate"].ToString()!;
        
        return await portfolioComparisonTool.ComparePortfolioPerformance(accountId, startDate, endDate, cancellationToken);
    }

    private async Task<object> ExecuteGetMarketContext(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ExtractStringArrayFromJsonParameter(parameters["tickers"]);
        var date = parameters["date"].ToString()!;
        
        return await marketIntelligenceTool.GetMarketContext(tickers, date, cancellationToken);
    }

    private async Task<object> ExecuteGetMarketSentiment(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var date = parameters["date"].ToString()!;
        
        return await marketIntelligenceTool.GetMarketSentiment(date, cancellationToken);
    }

    private async Task<object> ExecuteGetRealTimePrices(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ExtractStringArrayFromJsonParameter(parameters["tickers"]);
        
        logger.LogInformation("Executing GetRealTimePrices for tickers: {Tickers}", string.Join(", ", tickers));
        
        var prices = await eodMarketDataTool.GetRealTimePricesAsync(tickers, cancellationToken);
        
        return new
        {
            Tickers = tickers,
            Prices = prices,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<object> ExecuteSearchRecentNews(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var tickers = ExtractStringArrayFromJsonParameter(parameters["tickers"]);
        var companyNames = parameters.TryGetValue("companyNames", out var cn) ? cn.ToString()! : string.Empty;

        return await tavilySearchTool.SearchRecentNews(tickers, companyNames, cancellationToken);
    }

    private async Task<object> ExecuteResearchCompanyFundamentals(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var ticker = parameters["ticker"].ToString()!;
        var companyName = parameters.TryGetValue("companyName", out var cn) ? cn.ToString()! : ticker;

        return await tavilySearchTool.ResearchCompanyFundamentals(ticker, companyName, cancellationToken);
    }

    private async Task<object> ExecuteGetCompanyOverview(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var ticker = parameters["ticker"].ToString()!;
        var companyName = parameters.TryGetValue("companyName", out var cn) ? cn.ToString()! : ticker;

        return await tavilySearchTool.GetCompanyOverview(ticker, companyName, cancellationToken);
    }

    private async Task<object> ExecuteGetMarketOverview(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var focus = parameters.TryGetValue("focus", out var f) ? f?.ToString() : null;

        return await tavilySearchTool.GetMarketOverview(focus, cancellationToken);
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

    /// <summary>
    /// Dispose of the MCP server resources properly
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            logger.LogInformation("Cleaning up custom MCP server service");
            _isInitialized = false;
            logger.LogInformation("Custom MCP server service cleaned up successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up custom MCP server service");
        }

        _initializationSemaphore.Dispose();
        await Task.CompletedTask;
    }

    #endregion
}