using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        // NOTE: AI service implementations are now registered in Application layer
        // This extension now only handles API-layer specific configurations
        // HttpClient management is handled in the Application layer service registrations

        return services;
    }

    /// <summary>
    /// Initialize AI services that require startup configuration
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Task for async initialization</returns>
    public static async Task InitializeAiServicesAsync(this WebApplication app)
    {
        // For now, just return successfully to avoid startup issues
        // We'll implement proper initialization later when dependencies are stable
        await Task.CompletedTask;
        
        // Optional: Add basic logging if available
        try
        {
            using var scope = app.Services.CreateScope();
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                var logger = loggerFactory.CreateLogger("AiServiceInitialization");
                logger.LogInformation("AI services initialization skipped - services will initialize on-demand");
            }
        }
        catch
        {
            // Ignore any errors during optional logging
        }
    }
}