using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Api.Services.Ai;

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

        // HTTP client for market intelligence service
        services.AddHttpClient<MarketIntelligenceService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "PortfolioManager/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

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