using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FtoConsulting.PortfolioManager.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<IPortfolioIngest, PortfolioIngestService>();
        services.AddScoped<IHoldingsRetrieval, HoldingsRetrievalService>();
        services.AddScoped<IPriceFetching, PriceFetchingService>();
        
        // Register MediatR for CQRS
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));
        
        return services;
    }
}