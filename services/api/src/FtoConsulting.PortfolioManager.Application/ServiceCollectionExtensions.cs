using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Guardrails;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Application.Services.Memory;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register core application services
        services.AddScoped<IPortfolioIngest, PortfolioIngestService>();
        services.AddScoped<IHoldingService>(serviceProvider =>
        {
            var holdingRepository = serviceProvider.GetRequiredService<IHoldingRepository>();
            var portfolioRepository = serviceProvider.GetRequiredService<IPortfolioRepository>();
            var instrumentRepository = serviceProvider.GetRequiredService<IInstrumentRepository>();
            var instrumentManagementService = serviceProvider.GetRequiredService<IInstrumentManagementService>();
            var pricingCalculationHelper = serviceProvider.GetRequiredService<IPricingCalculationHelper>();
            var pricingCalculationService = serviceProvider.GetRequiredService<IPricingCalculationService>();
            var currencyConversionService = serviceProvider.GetRequiredService<ICurrencyConversionService>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            var logger = serviceProvider.GetRequiredService<ILogger<HoldingService>>();
            
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
            
            return new HoldingService(
                holdingRepository,
                portfolioRepository,
                instrumentRepository,
                instrumentManagementService,
                pricingCalculationHelper,
                pricingCalculationService,
                unitOfWork,
                logger,
                eodMarketDataToolFactory);
        });
        services.AddScoped<IPriceFetching, PriceFetchingService>();
        services.AddScoped<IHoldingRevaluationService, HoldingRevaluationService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IPricingCalculationService, PricingCalculationService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        // Register new holding management services
        services.AddScoped<IInstrumentManagementService, InstrumentManagementService>();
        services.AddScoped<IPricingCalculationHelper, PricingCalculationHelper>();
        
        // Register OpenAIClient pointing to the Foundry /openai/v1/ endpoint — works for all deployed models
        services.AddScoped<OpenAIClient>(serviceProvider =>
        {
            var azureFoundryOptions = serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>().Value;
            
            if (string.IsNullOrEmpty(azureFoundryOptions.FoundryProjectEndpoint) || string.IsNullOrEmpty(azureFoundryOptions.ApiKey))
            {
                return null!;
            }
            
            return new OpenAIClient(
                new ApiKeyCredential(azureFoundryOptions.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(azureFoundryOptions.FoundryProjectEndpoint) });
        });
        
        // Register AI chat service abstraction (used for memory extraction and market intelligence)
        services.AddScoped<IAiChatService>(serviceProvider =>
        {
            var openAiClient = serviceProvider.GetService<OpenAIClient>();
            if (openAiClient == null)
            {
                return null!;
            }
            
            var logger = serviceProvider.GetRequiredService<ILogger<AzureOpenAiChatService>>();
            var azureFoundryOptions = serviceProvider.GetRequiredService<IOptions<AzureFoundryOptions>>();
            return new AzureOpenAiChatService(openAiClient, azureFoundryOptions, logger);
        });
        
        // Register AI services (now in correct Application layer)
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IPortfolioAnalysisService, PortfolioAnalysisService>();
        
        // Register AI Guardrails
        services.AddScoped<InputValidationGuardrails>();
        services.AddScoped<OutputValidationGuardrails>();
        services.AddScoped<AgentFrameworkGuardrails>();
        
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
        services.AddScoped<TavilySearchTool>();

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
        
        // Register automated background services
        // Note: AutomatedRevaluationBackgroundService removed - now triggered by Azure Logic App
        services.AddHostedService<McpServerInitializationService>();
        
        return services;
    }
}
