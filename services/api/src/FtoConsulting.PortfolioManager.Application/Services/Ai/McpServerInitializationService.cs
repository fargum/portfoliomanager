using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Background service that initializes the MCP server during application startup
/// Ensures proper configuration validation and external service pre-initialization
/// </summary>
public class McpServerInitializationService(
    IServiceProvider serviceProvider,
    ILogger<McpServerInitializationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting MCP server initialization");

        try
        {
            // Create a scope to get the MCP server service
            var scope = serviceProvider.CreateScope();
            try
            {
                var mcpServerService = scope.ServiceProvider.GetRequiredService<IMcpServerService>();

                // Initialize the MCP server
                await mcpServerService.InitializeAsync(stoppingToken);

                logger.LogInformation("MCP server initialization completed successfully");
            }
            finally
            {
                if (scope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    scope.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("MCP server initialization was cancelled during application shutdown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize MCP server during startup. The application will continue but MCP functionality may be degraded");
            // Don't throw - let the application continue to start even if MCP initialization fails
            // The MCP service will handle initialization on first use if this fails
        }

        // This service only needs to run once at startup, so we complete immediately
        // The actual MCP service will handle ongoing operations
    }
}