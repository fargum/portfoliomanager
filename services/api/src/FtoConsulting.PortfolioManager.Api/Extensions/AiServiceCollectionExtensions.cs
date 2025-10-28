using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Api.Services.Ai;
using FtoConsulting.PortfolioManager.Api.Services.Ai.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace FtoConsulting.PortfolioManager.Api.Extensions;

/// <summary>
/// Extension methods for registering AI services
/// </summary>
public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Add AI services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        // AI service registrations
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IPortfolioAnalysisService, PortfolioAnalysisService>();
        services.AddScoped<IMarketIntelligenceService, MarketIntelligenceService>();
        services.AddScoped<IMcpServerService, McpServerService>();

        // MCP tool registrations
        services.AddScoped<PortfolioHoldingsTool>();
        services.AddScoped<PortfolioAnalysisTool>();
        services.AddScoped<MarketIntelligenceTool>();
        services.AddScoped<PortfolioComparisonTool>();

        // HTTP client for market intelligence service
        services.AddHttpClient<MarketIntelligenceService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "PortfolioManager/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// Add MCP server to the dependency injection container using official Microsoft packages
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddMcpPortfolioServer(this IServiceCollection services)
    {
        // Create MCP tools using AIFunctionFactory with consistent naming
        var portfolioHoldingsTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (PortfolioHoldingsTool tool, int accountId, string date, CancellationToken cancellationToken) =>
                    tool.GetPortfolioHoldings(accountId, date, cancellationToken),
                name: "GetPortfolioHoldings",
                description: "Retrieve portfolio holdings for a specific account and date"));

        var portfolioAnalysisTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (PortfolioAnalysisTool tool, int accountId, string analysisDate, CancellationToken cancellationToken) =>
                    tool.AnalyzePortfolioPerformance(accountId, analysisDate, cancellationToken),
                name: "AnalyzePortfolioPerformance", 
                description: "Analyze portfolio performance and generate insights for a specific date"));

        var portfolioComparisonTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (PortfolioComparisonTool tool, int accountId, string startDate, string endDate, CancellationToken cancellationToken) =>
                    tool.ComparePortfolioPerformance(accountId, startDate, endDate, cancellationToken),
                name: "ComparePortfolioPerformance",
                description: "Compare portfolio performance between two dates"));

        var marketContextTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (MarketIntelligenceTool tool, string[] tickers, string date, CancellationToken cancellationToken) =>
                    tool.GetMarketContext(tickers, date, cancellationToken),
                name: "GetMarketContext",
                description: "Get market context and news for specific stock tickers"));

        var financialNewsTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (MarketIntelligenceTool tool, string[] tickers, string fromDate, string toDate, CancellationToken cancellationToken) =>
                    tool.SearchFinancialNews(tickers, fromDate, toDate, cancellationToken),
                name: "SearchFinancialNews",
                description: "Search for financial news related to specific tickers within a date range"));

        var marketSentimentTool = McpServerTool.Create(
            AIFunctionFactory.Create(
                method: (MarketIntelligenceTool tool, string date, CancellationToken cancellationToken) =>
                    tool.GetMarketSentiment(date, cancellationToken),
                name: "GetMarketSentiment",
                description: "Get overall market sentiment and indicators for a specific date"));

        // Configure MCP Server using the official packages for web applications
        services
            .AddMcpServer()
            .WithHttpTransport() // Use HTTP transport for web applications
            .WithTools([
                portfolioHoldingsTool, 
                portfolioAnalysisTool, 
                portfolioComparisonTool,
                marketContextTool,
                financialNewsTool,
                marketSentimentTool
            ]);

        return services;
    }

    /// <summary>
    /// Initialize AI services that require startup configuration
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Task for async initialization</returns>
    public static async Task InitializeAiServicesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var mcpServerService = scope.ServiceProvider.GetRequiredService<IMcpServerService>();
        
        // Initialize MCP server with available tools
        await mcpServerService.InitializeAsync();
    }
}