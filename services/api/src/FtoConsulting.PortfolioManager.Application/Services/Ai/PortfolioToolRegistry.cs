using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using Microsoft.Extensions.AI;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Centralized registry for all portfolio analysis tools
/// Eliminates duplication between MCP server and AI orchestration service
/// </summary>
public static class PortfolioToolRegistry
{
    /// <summary>
    /// Core tool definitions shared across all services
    /// </summary>
    public static readonly PortfolioToolDefinition[] Tools = new[]
    {
        new PortfolioToolDefinition(
            Name: "GetPortfolioHoldings",
            Description: "Retrieve portfolio holdings for a specific account and date. For current/today performance, use 'today' or current date to get real-time data.",
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["accountId"] = new ToolParameterDefinition("integer", "Account ID", true),
                ["date"] = new ToolParameterDefinition("string", "Date for holdings analysis. Use 'today' for real-time data, or specify historical date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)", true)
            },
            Category: "Portfolio Data"
        ),
        
        new PortfolioToolDefinition(
            Name: "AnalyzePortfolioPerformance", 
            Description: "Analyze portfolio performance and generate insights for a specific date. For current/today performance, use 'today' or current date to get real-time analysis.",
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["accountId"] = new ToolParameterDefinition("integer", "Account ID", true),
                ["analysisDate"] = new ToolParameterDefinition("string", "Analysis date. Use 'today' for real-time analysis, or specify historical date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)", true)
            },
            Category: "Portfolio Analysis"
        ),
        
        new PortfolioToolDefinition(
            Name: "ComparePortfolioPerformance",
            Description: "Compare portfolio performance between two dates", 
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["accountId"] = new ToolParameterDefinition("integer", "Account ID", true),
                ["startDate"] = new ToolParameterDefinition("string", "Start date in YYYY-MM-DD format", true),
                ["endDate"] = new ToolParameterDefinition("string", "End date in YYYY-MM-DD format", true)
            },
            Category: "Portfolio Analysis"
        ),
        
        new PortfolioToolDefinition(
            Name: "GetMarketContext",
            Description: "Get market context and news for specific stock tickers",
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["tickers"] = new ToolParameterDefinition("array", "List of stock tickers", true, "string"),
                ["date"] = new ToolParameterDefinition("string", "Date for market analysis in YYYY-MM-DD format", true)
            },
            Category: "Market Intelligence"
        ),
        
        new PortfolioToolDefinition(
            Name: "SearchFinancialNews",
            Description: "Search for financial news related to specific tickers within a date range",
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["tickers"] = new ToolParameterDefinition("array", "List of stock tickers", true, "string"),
                ["fromDate"] = new ToolParameterDefinition("string", "Start date in YYYY-MM-DD format", true),
                ["toDate"] = new ToolParameterDefinition("string", "End date in YYYY-MM-DD format", true)
            },
            Category: "Market Intelligence"
        ),
        
        new PortfolioToolDefinition(
            Name: "GetMarketSentiment",
            Description: "Get overall market sentiment and indicators for a specific date",
            Parameters: new Dictionary<string, ToolParameterDefinition>
            {
                ["date"] = new ToolParameterDefinition("string", "Date for sentiment analysis in YYYY-MM-DD format", true)
            },
            Category: "Market Intelligence"
        )
    };

    /// <summary>
    /// Convert to MCP tool definitions for the MCP server
    /// </summary>
    public static IEnumerable<McpToolDefinition> GetMcpToolDefinitions()
    {
        return Tools.Select(tool => new McpToolDefinition(
            Name: tool.Name,
            Description: tool.Description,
            Schema: CreateMcpSchema(tool.Parameters)
        ));
    }

    /// <summary>
    /// Convert to AI tool DTOs for API responses
    /// </summary>
    public static IEnumerable<AiToolDto> GetAiToolDtos()
    {
        return Tools.Select(tool => new AiToolDto(
            Name: tool.Name,
            Description: tool.Description,
            Parameters: CreateAiToolParameters(tool.Parameters),
            Category: tool.Category
        ));
    }

    /// <summary>
    /// Create AI functions for the Microsoft Agent Framework
    /// </summary>
    public static IEnumerable<AITool> CreateAiFunctions(Func<string, Dictionary<string, object>, Task<object>> toolExecutor)
    {
        var functions = new List<AITool>
        {
            AIFunctionFactory.Create(
                method: (int accountId, string date) => toolExecutor("GetPortfolioHoldings", new Dictionary<string, object> { ["accountId"] = accountId, ["date"] = date }),
                name: "GetPortfolioHoldings",
                description: "Retrieve portfolio holdings for a specific account and date. For current/today performance, use 'today' or current date to get real-time data."),

            AIFunctionFactory.Create(
                method: (int accountId, string analysisDate) => toolExecutor("AnalyzePortfolioPerformance", new Dictionary<string, object> { ["accountId"] = accountId, ["analysisDate"] = analysisDate }),
                name: "AnalyzePortfolioPerformance",
                description: "Analyze portfolio performance and generate insights for a specific date. For current/today performance, use 'today' or current date to get real-time analysis."),

            AIFunctionFactory.Create(
                method: (int accountId, string startDate, string endDate) => toolExecutor("ComparePortfolioPerformance", new Dictionary<string, object> { ["accountId"] = accountId, ["startDate"] = startDate, ["endDate"] = endDate }),
                name: "ComparePortfolioPerformance",
                description: "Compare portfolio performance between two dates"),

            AIFunctionFactory.Create(
                method: (string[] tickers, string date) => toolExecutor("GetMarketContext", new Dictionary<string, object> { ["tickers"] = tickers, ["date"] = date }),
                name: "GetMarketContext",
                description: "Get market context and news for specific stock tickers"),

            AIFunctionFactory.Create(
                method: (string[] tickers, string fromDate, string toDate) => toolExecutor("SearchFinancialNews", new Dictionary<string, object> { ["tickers"] = tickers, ["fromDate"] = fromDate, ["toDate"] = toDate }),
                name: "SearchFinancialNews",
                description: "Search for financial news related to specific tickers within a date range"),

            AIFunctionFactory.Create(
                method: (string date) => toolExecutor("GetMarketSentiment", new Dictionary<string, object> { ["date"] = date }),
                name: "GetMarketSentiment",
                description: "Get overall market sentiment and indicators for a specific date")
        };

        return functions;
    }

    /// <summary>
    /// Create MCP schema format from parameter definitions
    /// </summary>
    private static Dictionary<string, object> CreateMcpSchema(Dictionary<string, ToolParameterDefinition> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var (paramName, paramDef) in parameters)
        {
            var property = new Dictionary<string, object>
            {
                ["type"] = paramDef.Type,
                ["description"] = paramDef.Description
            };

            if (paramDef.Type == "array" && !string.IsNullOrEmpty(paramDef.ItemType))
            {
                property["items"] = new { type = paramDef.ItemType };
            }

            properties[paramName] = property;

            if (paramDef.Required)
            {
                required.Add(paramName);
            }
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required.ToArray()
        };
    }

    /// <summary>
    /// Create AI tool parameter format from parameter definitions
    /// </summary>
    private static Dictionary<string, object> CreateAiToolParameters(Dictionary<string, ToolParameterDefinition> parameters)
    {
        var result = new Dictionary<string, object>();

        foreach (var (paramName, paramDef) in parameters)
        {
            var parameter = new Dictionary<string, object>
            {
                ["type"] = paramDef.Type,
                ["description"] = paramDef.Description
            };

            if (paramDef.Type == "array" && !string.IsNullOrEmpty(paramDef.ItemType))
            {
                parameter["items"] = new { type = paramDef.ItemType };
            }

            result[paramName] = parameter;
        }

        return result;
    }
}

/// <summary>
/// Core tool definition with all metadata
/// </summary>
public record PortfolioToolDefinition(
    string Name,
    string Description,
    Dictionary<string, ToolParameterDefinition> Parameters,
    string Category
);

/// <summary>
/// Parameter definition for tools
/// </summary>
public record ToolParameterDefinition(
    string Type,
    string Description,
    bool Required,
    string? ItemType = null
);
