using System.Reflection;
using FtoConsulting.PortfolioManager.Application;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace FtoConsulting.PortfolioManager.Evaluation;

public static class EvaluationHost
{
    public static ServiceProvider Build(IConfiguration config, PortfolioFixture? fixture, Func<string, Microsoft.Extensions.AI.IChatClient>? modelClientFactory = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(config);
        services.AddHttpContextAccessor();
        services.Configure<AzureFoundryOptions>(config.GetSection(AzureFoundryOptions.SectionName));
        services.Configure<EodApiOptions>(config.GetSection(EodApiOptions.SectionName));
        services.Configure<TavilyOptions>(config.GetSection(TavilyOptions.SectionName));
        services.Configure<PortfolioReportOptions>(config.GetSection(PortfolioReportOptions.SectionName));
        var connection = fixture != null ? "Host=localhost;Port=1;Database=unused;Username=unused;Timeout=1" :
            config.GetConnectionString("EvaluationDatabase") ?? throw new InvalidOperationException("Live collection requires ConnectionStrings__EvaluationDatabase for a test database");
        services.AddDbContext<PortfolioManagerDbContext>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "app")));
        services.AddApplicationServices();
        if (modelClientFactory != null) services.AddSingleton(modelClientFactory);
        services.AddInfrastructureServices();
        if (fixture != null)
        {
            services.AddSingleton<IHoldingService>(new FixtureHoldingService(fixture));
            services.AddScoped<McpServerService>();
            services.AddScoped<IMcpServerService>(p => new FixtureToolBoundary(p.GetRequiredService<McpServerService>()));
            services.AddSingleton(DisabledPersistence.Create<ISecurityIncidentRepository>());
            services.AddSingleton(DisabledPersistence.Create<IUnitOfWork>());
        }
        // Build only. Do not start hosted services (MCP startup, reports, or schedulers) in the evaluation process.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static TracerProvider CreateTelemetry(IConfiguration config)
    {
        var builder = Sdk.CreateTracerProviderBuilder().SetSampler(new AlwaysOnSampler())
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PortfolioManager.Evaluation"))
            .AddSource("PortfolioManager.*");
        if (config["OTEL_EXPORTER_OTLP_ENDPOINT"] is { Length: > 0 } endpoint)
            builder.AddOtlpExporter(o => o.Endpoint = new Uri(endpoint));
        if (config["APPLICATIONINSIGHTS_CONNECTION_STRING"] is { Length: > 0 } connection)
            builder.AddAzureMonitorTraceExporter(o => o.ConnectionString = connection);
        return builder.Build();
    }
}

/// <summary>Reject unconfigured external dependencies before they can make network requests in fixture runs.</summary>
internal sealed class FixtureToolBoundary(McpServerService inner) : IMcpServerService
{
    public Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        if (toolName is not ("GetPortfolioHoldings" or "GetHoldingByTicker" or "AnalyzePortfolioPerformance" or "ComparePortfolioPerformance"))
            throw new InvalidOperationException($"Tool {toolName} has no fixture; use the live integration suite for external tools");
        return inner.ExecuteToolAsync(toolName, parameters, cancellationToken);
    }
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IEnumerable<McpToolDefinition>> GetAvailableToolsAsync() => inner.GetAvailableToolsAsync();
    public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    public Task<object> CallMcpToolAsync(string serverId, string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // DI owns inner.
}

public class DisabledPersistence : DispatchProxy
{
    public static T Create<T>() where T : class => Create<T, DisabledPersistence>();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "Dispose") return null;
        if (targetMethod?.Name == "DisposeAsync") return ValueTask.CompletedTask;
        throw new InvalidOperationException("Persistence is disabled in fixture evaluation");
    }
}
