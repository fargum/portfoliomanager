using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of portfolio analysis service for AI-powered insights
/// </summary>
public class PortfolioAnalysisService : IPortfolioAnalysisService
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly ILogger<PortfolioAnalysisService> _logger;

    public PortfolioAnalysisService(
        IHoldingsRetrieval holdingsRetrieval,
        ILogger<PortfolioAnalysisService> logger)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _logger = logger;
    }

    public async Task<PortfolioAnalysisDto> AnalyzePortfolioPerformanceAsync(int accountId, DateTime analysisDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing portfolio performance for account {AccountId} on {Date}", accountId, analysisDate);

            // Get current holdings
            var holdings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(
                accountId, DateOnly.FromDateTime(analysisDate), cancellationToken);

            if (!holdings.Any())
            {
                _logger.LogWarning("No holdings found for account {AccountId} on {Date}", accountId, analysisDate);
                return CreateEmptyAnalysis(accountId, analysisDate);
            }

            // Convert holdings to performance DTOs
            var holdingPerformance = holdings.Select(h => new HoldingPerformanceDto(
                Ticker: h.Instrument?.Ticker ?? "UNKNOWN",
                InstrumentName: h.Instrument?.Name ?? "Unknown Instrument",
                UnitAmount: h.UnitAmount,
                CurrentValue: h.CurrentValue,
                BoughtValue: h.BoughtValue,
                DayChange: h.DailyProfitLoss,
                DayChangePercentage: h.DailyProfitLossPercentage / 100m,
                TotalReturn: h.CurrentValue - h.BoughtValue,
                TotalReturnPercentage: h.BoughtValue > 0 ? (h.CurrentValue - h.BoughtValue) / h.BoughtValue : 0
            )).ToList();

            // Calculate portfolio totals
            var totalValue = holdingPerformance.Sum(h => h.CurrentValue);
            var totalDayChange = holdingPerformance.Sum(h => h.DayChange);
            var totalDayChangePercentage = totalValue > 0 ? totalDayChange / (totalValue - totalDayChange) : 0;

            // Generate performance metrics
            var metrics = CalculatePerformanceMetrics(holdingPerformance);

            return new PortfolioAnalysisDto(
                AccountId: accountId,
                AnalysisDate: analysisDate,
                TotalValue: totalValue,
                DayChange: totalDayChange,
                DayChangePercentage: totalDayChangePercentage,
                HoldingPerformance: holdingPerformance,
                Metrics: metrics
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing portfolio performance for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<PerformanceComparisonDto> ComparePerformanceAsync(int accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Comparing portfolio performance for account {AccountId} between {StartDate} and {EndDate}", 
                accountId, startDate, endDate);

            // Get holdings for both dates
            var startHoldings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(
                accountId, DateOnly.FromDateTime(startDate), cancellationToken);
            var endHoldings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(
                accountId, DateOnly.FromDateTime(endDate), cancellationToken);

            var startValue = startHoldings.Sum(h => h.CurrentValue);
            var endValue = endHoldings.Sum(h => h.CurrentValue);

            var totalChange = endValue - startValue;
            var totalChangePercentage = startValue > 0 ? totalChange / startValue : 0;

            // Compare individual holdings
            var holdingComparisons = CompareHoldings(startHoldings, endHoldings);

            // Generate insights
            var insights = GenerateComparisonInsights(holdingComparisons, totalChangePercentage);

            return new PerformanceComparisonDto(
                AccountId: accountId,
                StartDate: startDate,
                EndDate: endDate,
                StartValue: startValue,
                EndValue: endValue,
                TotalChange: totalChange,
                TotalChangePercentage: totalChangePercentage,
                HoldingComparisons: holdingComparisons,
                Insights: insights
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing portfolio performance for account {AccountId}", accountId);
            throw;
        }
    }

    private PortfolioAnalysisDto CreateEmptyAnalysis(int accountId, DateTime analysisDate)
    {
        return new PortfolioAnalysisDto(
            AccountId: accountId,
            AnalysisDate: analysisDate,
            TotalValue: 0,
            DayChange: 0,
            DayChangePercentage: 0,
            HoldingPerformance: Enumerable.Empty<HoldingPerformanceDto>(),
            Metrics: new PerformanceMetricsDto(
                TotalReturn: 0,
                TotalReturnPercentage: 0,
                TopPerformers: Enumerable.Empty<string>(),
                BottomPerformers: Enumerable.Empty<string>()
            )
        );
    }

    private PerformanceMetricsDto CalculatePerformanceMetrics(IEnumerable<HoldingPerformanceDto> holdings)
    {
        var holdingsList = holdings.ToList();
        
        var totalReturn = holdingsList.Sum(h => h.TotalReturn);
        var totalBoughtValue = holdingsList.Sum(h => h.BoughtValue);
        var totalReturnPercentage = totalBoughtValue > 0 ? totalReturn / totalBoughtValue : 0;

        // Simple volatility estimate based on day changes
        var dayChanges = holdingsList.Select(h => h.DayChangePercentage).ToList();
        var avgDayChange = dayChanges.Any() ? dayChanges.Average() : 0;


        var topPerformers = holdingsList
            .Where(h => h.DayChangePercentage > 0)
            .OrderByDescending(h => h.DayChangePercentage)
            .Take(5)
            .Select(h => h.Ticker);

        var bottomPerformers = holdingsList
            .Where(h => h.DayChangePercentage < 0)
            .OrderBy(h => h.DayChangePercentage)
            .Take(5)
            .Select(h => h.Ticker);

        return new PerformanceMetricsDto(
            TotalReturn: totalReturn,
            TotalReturnPercentage: totalReturnPercentage,
            TopPerformers: topPerformers,
            BottomPerformers: bottomPerformers
        );
    }

    private IEnumerable<HoldingComparisonDto> CompareHoldings(
        IEnumerable<Domain.Entities.Holding> startHoldings, 
        IEnumerable<Domain.Entities.Holding> endHoldings)
    {
        // Create composite keys using ticker + platform to handle same ticker across different platforms
        var startDict = startHoldings.ToDictionary(
            h => $"{h.Instrument?.Ticker ?? "UNKNOWN"}|{h.Platform?.Name ?? "UNKNOWN"}", 
            h => h);
        var endDict = endHoldings.ToDictionary(
            h => $"{h.Instrument?.Ticker ?? "UNKNOWN"}|{h.Platform?.Name ?? "UNKNOWN"}", 
            h => h);

        var allCompositeKeys = startDict.Keys.Union(endDict.Keys);

        return allCompositeKeys.Select(compositeKey =>
        {
            var startHolding = startDict.GetValueOrDefault(compositeKey);
            var endHolding = endDict.GetValueOrDefault(compositeKey);

            var ticker = compositeKey.Split('|')[0];
            var platform = compositeKey.Split('|')[1];

            var startValue = startHolding?.CurrentValue ?? 0;
            var endValue = endHolding?.CurrentValue ?? 0;
            var change = endValue - startValue;
            var changePercentage = startValue > 0 ? change / startValue : 0;

            return new HoldingComparisonDto(
                Ticker: platform != "UNKNOWN" ? $"{ticker} ({platform})" : ticker,
                InstrumentName: endHolding?.Instrument?.Name ?? startHolding?.Instrument?.Name ?? "Unknown",
                StartValue: startValue,
                EndValue: endValue,
                Change: change,
                ChangePercentage: changePercentage
            );
        });
    }

    private ComparisonInsightsDto GenerateComparisonInsights(
        IEnumerable<HoldingComparisonDto> holdingComparisons, 
        decimal totalChangePercentage)
    {
        var comparisons = holdingComparisons.ToList();

        var overallTrend = totalChangePercentage switch
        {
            > 0.1m => "Strong Growth",
            > 0.05m => "Moderate Growth",
            > 0 => "Slight Growth",
            > -0.05m => "Slight Decline",
            > -0.1m => "Moderate Decline",
            _ => "Significant Decline"
        };

        var keyDrivers = comparisons
            .Where(c => Math.Abs(c.ChangePercentage) > 0.05m)
            .OrderByDescending(c => Math.Abs(c.Change))
            .Take(3)
            .Select(c => $"{c.Ticker}: {c.ChangePercentage:P2}")
            .ToList();

        var riskFactors = comparisons
            .Where(c => c.ChangePercentage < -0.1m)
            .Select(c => $"High volatility in {c.Ticker}")
            .ToList();

        var opportunities = comparisons
            .Where(c => c.ChangePercentage > 0.1m)
            .Select(c => $"Strong momentum in {c.Ticker}")
            .ToList();

        return new ComparisonInsightsDto(
            OverallTrend: overallTrend,
            KeyDrivers: keyDrivers,
            RiskFactors: riskFactors,
            Opportunities: opportunities
        );
    }
}