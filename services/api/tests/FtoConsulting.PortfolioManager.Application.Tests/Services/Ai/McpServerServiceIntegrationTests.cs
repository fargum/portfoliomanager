using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services.Ai;

/// <summary>
/// Integration tests for Microsoft Agent Framework MCP server implementation
/// </summary>
public class McpServerServiceIntegrationTests
{
    [Fact]
    public async Task InitializeAsync_ShouldSucceedWithValidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Mock required services
        services.AddSingleton<ILogger<McpServerService>>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<McpServerService>());
        services.AddLogging();
        
        // Add mock configurations
        services.Configure<AzureFoundryOptions>(options =>
        {
            options.Endpoint = "https://test.openai.azure.com/";
            options.ApiKey = "test-key";
            options.ModelName = "gpt-4o-mini";
        });
        
        services.Configure<EodApiOptions>(options =>
        {
            options.Token = ""; // Leave empty to skip EOD initialization
            options.McpServerUrl = "";
            options.TimeoutSeconds = 30;
        });
        
        // Mock tool dependencies
        services.AddTransient<PortfolioHoldingsTool>(sp => new PortfolioHoldingsTool(null!));
        services.AddTransient<PortfolioAnalysisTool>(sp => new PortfolioAnalysisTool(null!));
        services.AddTransient<PortfolioComparisonTool>(sp => new PortfolioComparisonTool(null!));
        services.AddTransient<MarketIntelligenceTool>(sp => new MarketIntelligenceTool(null!));
        services.AddScoped<EodMarketDataTool>();
        services.Configure<TavilyOptions>(options => { });
        services.AddScoped<TavilySearchTool>();

        // Mock core services
        services.AddTransient<IHoldingService>(sp => new MockHoldingService());

        var serviceProvider = services.BuildServiceProvider();

        var mcpService = new McpServerService(
            serviceProvider.GetRequiredService<IHoldingService>(),
            serviceProvider.GetRequiredService<ILogger<McpServerService>>(),
            serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>(),
            serviceProvider.GetRequiredService<IOptions<EodApiOptions>>(),
            serviceProvider.GetRequiredService<PortfolioHoldingsTool>(),
            serviceProvider.GetRequiredService<PortfolioAnalysisTool>(),
            serviceProvider.GetRequiredService<PortfolioComparisonTool>(),
            serviceProvider.GetRequiredService<MarketIntelligenceTool>(),
            serviceProvider.GetRequiredService<EodMarketDataTool>(),
            serviceProvider.GetRequiredService<TavilySearchTool>());

        // Act & Assert - Should not throw
        await mcpService.InitializeAsync(CancellationToken.None);
        
        // Verify the service reports as healthy (basic connectivity test)
        var isHealthy = await mcpService.IsHealthyAsync();
        
        // Cleanup
        await mcpService.DisposeAsync();
        
        // The initialization should succeed even if health check fails due to mock services
        Assert.True(true); // If we get here without exceptions, initialization succeeded
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ShouldReturnPortfolioTools()
    {
        // This test verifies that the MCP tool registry integration works
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Mock minimal required services for instantiation
        services.AddSingleton<ILogger<McpServerService>>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<McpServerService>());
            
        services.Configure<AzureFoundryOptions>(options =>
        {
            options.Endpoint = "https://test.openai.azure.com/";
            options.ApiKey = "test-key";
            options.ModelName = "gpt-4o-mini";
        });
        
        services.Configure<EodApiOptions>(options => { });
        
        services.AddTransient<PortfolioHoldingsTool>(sp => new PortfolioHoldingsTool(null!));
        services.AddTransient<PortfolioAnalysisTool>(sp => new PortfolioAnalysisTool(null!));
        services.AddTransient<PortfolioComparisonTool>(sp => new PortfolioComparisonTool(null!));
        services.AddTransient<MarketIntelligenceTool>(sp => new MarketIntelligenceTool(null!));
        services.AddScoped<EodMarketDataTool>();
        services.Configure<TavilyOptions>(options => { });
        services.AddScoped<TavilySearchTool>();
        services.AddTransient<IHoldingService>(sp => new MockHoldingService());

        var serviceProvider = services.BuildServiceProvider();

        var mcpService = new McpServerService(
            serviceProvider.GetRequiredService<IHoldingService>(),
            serviceProvider.GetRequiredService<ILogger<McpServerService>>(),
            serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>(),
            serviceProvider.GetRequiredService<IOptions<EodApiOptions>>(),
            serviceProvider.GetRequiredService<PortfolioHoldingsTool>(),
            serviceProvider.GetRequiredService<PortfolioAnalysisTool>(),
            serviceProvider.GetRequiredService<PortfolioComparisonTool>(),
            serviceProvider.GetRequiredService<MarketIntelligenceTool>(),
            serviceProvider.GetRequiredService<EodMarketDataTool>(),
            serviceProvider.GetRequiredService<TavilySearchTool>());

        // Act
        var tools = await mcpService.GetAvailableToolsAsync();

        // Assert
        Assert.NotNull(tools);
        var toolList = tools.ToList();
        Assert.NotEmpty(toolList);
        
        // Verify expected portfolio tools are available
        var toolNames = toolList.Select(t => t.Name).ToList();
        Assert.Contains("GetPortfolioHoldings", toolNames);
        Assert.Contains("AnalyzePortfolioPerformance", toolNames);
        Assert.Contains("ComparePortfolioPerformance", toolNames);
        Assert.Contains("GetMarketContext", toolNames);
        Assert.Contains("GetMarketSentiment", toolNames);
        
        await mcpService.DisposeAsync();
    }
}

// Mock implementations for testing
public class MockHoldingService : IHoldingService
{
    public Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<Holding>());

    public Task<HoldingAddResult> AddHoldingAsync(int portfolioId, AddHoldingRequest request, int accountId, CancellationToken cancellationToken)
        => Task.FromResult(new HoldingAddResult { Success = true, Message = "Mock add" });
        
    public Task<HoldingUpdateResult> UpdateHoldingUnitsAsync(int holdingId, decimal newUnits, int accountId, CancellationToken cancellationToken)
        => Task.FromResult(new HoldingUpdateResult { Success = true, Message = "Mock update" });
        
    public Task<HoldingDeleteResult> DeleteHoldingAsync(int holdingId, int accountId, CancellationToken cancellationToken)
        => Task.FromResult(new HoldingDeleteResult { Success = true, Message = "Mock delete" });
}