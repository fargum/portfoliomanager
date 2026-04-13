using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories.Memory;
using FtoConsulting.PortfolioManager.Infrastructure.Services;
using FtoConsulting.PortfolioManager.Infrastructure.Services.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IHoldingRepository, HoldingRepository>();
        services.AddScoped<IInstrumentPriceRepository, InstrumentPriceRepository>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddScoped<ISecurityIncidentRepository, SecurityIncidentRepository>();
        
        // Register memory repositories
        services.AddScoped<IConversationThreadRepository, ConversationThreadRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IMemorySummaryRepository, MemorySummaryRepository>();
        services.AddTransient<Func<int, int?, ChatHistoryProvider>>(serviceProvider =>
            (accountId, threadId) =>
            {
                var dbContext = serviceProvider.GetRequiredService<PortfolioManagerDbContext>();
                var logger = serviceProvider.GetRequiredService<ILogger<PostgreSqlChatMessageStore>>();
                return new PostgreSqlChatMessageStore(dbContext, logger, accountId, threadId);
            });
            
        services.AddTransient<Func<int, IChatClient, AIContextProvider>>(serviceProvider =>
            (accountId, chatClient) =>
            {
                var dbContext = serviceProvider.GetRequiredService<PortfolioManagerDbContext>();
                var logger = serviceProvider.GetRequiredService<ILogger<PortfolioMemoryContextProvider>>();
                return new PortfolioMemoryContextProvider(dbContext, chatClient, logger, accountId);
            });
        
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        return services;
    }
}