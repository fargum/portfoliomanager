using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Application.Services.Memory;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register core application services
        services.AddScoped<IPortfolioIngest, PortfolioIngestService>();
        services.AddScoped<IHoldingsRetrieval>(serviceProvider =>
        {
            var holdingRepository = serviceProvider.GetRequiredService<IHoldingRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<HoldingsRetrievalService>>();
            var currencyConversionService = serviceProvider.GetRequiredService<ICurrencyConversionService>();
            var pricingCalculationService = serviceProvider.GetRequiredService<IPricingCalculationService>();
            
            // Create factory for EodMarketDataTool for real-time pricing
            Func<EodMarketDataTool>? eodMarketDataToolFactory = null;
            try 
            {
                eodMarketDataToolFactory = () => serviceProvider.GetRequiredService<EodMarketDataTool>();
            }
            catch
            {
                // If EodMarketDataTool can't be resolved, leave factory as null - service will work without real-time pricing
            }
            
            return new HoldingsRetrievalService(holdingRepository, logger, currencyConversionService, pricingCalculationService, eodMarketDataToolFactory);
        });
        services.AddScoped<IPriceFetching, PriceFetchingService>();
        services.AddScoped<IHoldingRevaluationService, HoldingRevaluationService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IPricingCalculationService, PricingCalculationService>();
        
        // Register Azure OpenAI client and AI chat service for dependency injection
        services.AddScoped<AzureOpenAIClient>(serviceProvider =>
        {
            var azureFoundryOptions = serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>().Value;
            
            if (string.IsNullOrEmpty(azureFoundryOptions.Endpoint) || string.IsNullOrEmpty(azureFoundryOptions.ApiKey))
            {
                // Return a null client if not configured - services will handle fallback
                return null!;
            }
            
            return new AzureOpenAIClient(
                new Uri(azureFoundryOptions.Endpoint),
                new AzureKeyCredential(azureFoundryOptions.ApiKey));
        });
        
        // Register AI chat service abstraction
        services.AddScoped<IAiChatService>(serviceProvider =>
        {
            var azureOpenAIClient = serviceProvider.GetService<AzureOpenAIClient>();
            if (azureOpenAIClient == null)
            {
                return null!; // Will cause services to use fallback behavior
            }
            
            var logger = serviceProvider.GetRequiredService<ILogger<AzureOpenAiChatService>>();
            var azureFoundryOptions = serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>();
            return new AzureOpenAiChatService(azureOpenAIClient, azureFoundryOptions, logger);
        });
        
        // Register AI services (now in correct Application layer)
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IPortfolioAnalysisService, PortfolioAnalysisService>();
        
        // Register memory services
        services.AddScoped<IConversationThreadService, ConversationThreadService>();
        services.AddScoped<IMemoryExtractionService, MemoryExtractionService>();
        
        // Register MCP Tools first (to avoid circular dependencies)
        services.AddScoped<PortfolioHoldingsTool>();
        services.AddScoped<PortfolioAnalysisTool>();
        services.AddScoped<PortfolioComparisonTool>();
        services.AddScoped<MarketIntelligenceTool>(serviceProvider =>
        {
            // Create factory for IMarketIntelligenceService to avoid circular dependency
            Func<IMarketIntelligenceService>? factory = null;
            try 
            {
                factory = () => serviceProvider.GetRequiredService<IMarketIntelligenceService>();
            }
            catch
            {
                // If IMarketIntelligenceService can't be resolved, leave factory as null
            }
            
            return new MarketIntelligenceTool(factory);
        });
        services.AddScoped<EodMarketDataTool>();
        
        // Register AI services
        services.AddSingleton<IAgentPromptService, AgentPromptService>();
        
        // Register MarketIntelligenceService with HttpClient factory
        services.AddScoped<IMarketIntelligenceService>(serviceProvider =>
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PortfolioManager/1.0");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var logger = serviceProvider.GetRequiredService<ILogger<MarketIntelligenceService>>();
            var aiChatService = serviceProvider.GetService<IAiChatService>();
            var mcpServerService = serviceProvider.GetService<IMcpServerService>();
            
            // Create factory for EodMarketDataTool
            Func<EodMarketDataTool>? eodMarketDataToolFactory = null;
            try 
            {
                eodMarketDataToolFactory = () => serviceProvider.GetRequiredService<EodMarketDataTool>();
            }
            catch
            {
                // If EodMarketDataTool can't be resolved, leave factory as null
            }
            
            return new MarketIntelligenceService(httpClient, logger, aiChatService, mcpServerService, eodMarketDataToolFactory);
        });
        
        services.AddScoped<IMcpServerService, McpServerService>();
        
        // Register MediatR for CQRS
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));
        
        return services;
    }
}
