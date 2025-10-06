using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

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
        
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        return services;
    }
}