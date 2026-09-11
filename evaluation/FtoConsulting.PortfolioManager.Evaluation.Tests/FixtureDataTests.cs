using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FtoConsulting.PortfolioManager.Evaluation.Tests;

public sealed class FixtureDataTests
{
    [Fact]
    public async Task RealAnalysisAndComparison_UseControlledPortfolioValues()
    {
        var fixture = new PortfolioFixture("v1", new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero), 42,
        [
            new(new(2026, 9, 10), "ALPHA", "Alpha", 10, 1000, 1100), new(new(2026, 9, 10), "BETA", "Beta", 20, 2000, 1900),
            new(new(2026, 9, 11), "ALPHA", "Alpha", 10, 1000, 1200, 100), new(new(2026, 9, 11), "BETA", "Beta", 20, 2000, 1800, -100)
        ]);
        await using var host = EvaluationHost.Build(new ConfigurationBuilder().Build(), fixture);
        await using var scope = host.CreateAsyncScope();
        var tools = scope.ServiceProvider.GetRequiredService<IMcpServerService>();
        var analysis = JsonSerializer.SerializeToElement(await tools.ExecuteToolAsync("AnalyzePortfolioPerformance", new() { ["accountId"] = 42, ["analysisDate"] = "2026-09-11" }));
        Assert.Equal(3000, analysis.GetProperty("Analysis").GetProperty("TotalValue").GetDecimal());
        var comparison = JsonSerializer.SerializeToElement(await tools.ExecuteToolAsync("ComparePortfolioPerformance", new() { ["accountId"] = 42, ["startDate"] = "2026-09-10", ["endDate"] = "2026-09-11" }));
        Assert.Equal(0, comparison.GetProperty("Comparison").GetProperty("TotalChange").GetDecimal());
        Assert.Equal(2, comparison.GetProperty("Comparison").GetProperty("HoldingComparisons").GetArrayLength());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.ExecuteToolAsync("GetRealTimePrices", new() { ["tickers"] = new[] { "MSFT" } }));
    }
}
