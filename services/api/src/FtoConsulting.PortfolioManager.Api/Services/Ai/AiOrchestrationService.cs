using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services;

namespace FtoConsulting.PortfolioManager.Api.Services.Ai;

/// <summary>
/// Implementation of AI orchestration service for portfolio queries
/// </summary>
public class AiOrchestrationService : IAiOrchestrationService
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;
    private readonly IMarketIntelligenceService _marketIntelligenceService;
    private readonly ILogger<AiOrchestrationService> _logger;

    public AiOrchestrationService(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioAnalysisService portfolioAnalysisService,
        IMarketIntelligenceService marketIntelligenceService,
        ILogger<AiOrchestrationService> logger)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _portfolioAnalysisService = portfolioAnalysisService;
        _marketIntelligenceService = marketIntelligenceService;
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessPortfolioQueryAsync(string query, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing portfolio query for account {AccountId}: {Query}", accountId, query);

            // Determine the analysis date based on query context
            var analysisDate = DetermineAnalysisDate(query);
            _logger.LogInformation("Using analysis date: {AnalysisDate} for account {AccountId}", analysisDate, accountId);

            // Get portfolio analysis for the determined date
            var portfolioAnalysis = await _portfolioAnalysisService.AnalyzePortfolioPerformanceAsync(
                accountId, analysisDate, cancellationToken);

            // Generate response based on query type and context
            var queryType = DetermineQueryType(query);
            var response = await GenerateResponseAsync(query, queryType, portfolioAnalysis, analysisDate, cancellationToken);

            return new ChatResponseDto(
                Response: response,
                QueryType: queryType,
                PortfolioSummary: new PortfolioSummaryDto(
                    AccountId: portfolioAnalysis.AccountId,
                    Date: portfolioAnalysis.AnalysisDate,
                    TotalValue: portfolioAnalysis.TotalValue,
                    DayChange: portfolioAnalysis.DayChange,
                    DayChangePercentage: portfolioAnalysis.DayChangePercentage,
                    HoldingsCount: portfolioAnalysis.HoldingPerformance.Count(),
                    TopHoldings: portfolioAnalysis.HoldingPerformance
                        .OrderByDescending(h => h.CurrentValue)
                        .Take(3)
                        .Select(h => h.Ticker)
                ),
                Insights: GenerateInsights(portfolioAnalysis)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing portfolio query for account {AccountId}: {Query}", accountId, query);
            
            // Return a helpful error response
            return new ChatResponseDto(
                Response: "I apologize, but I encountered an issue analyzing your portfolio. This might be because there's no data for the requested date. Please try asking about October 17th, 2025 (the main trading day with data available), or check if your account ID is correct.",
                QueryType: "Error"
            );
        }
    }

    public async Task<IEnumerable<AiToolDto>> GetAvailableToolsAsync()
    {
        await Task.CompletedTask; // Placeholder for async pattern

        return new[]
        {
            new AiToolDto(
                Name: "get_portfolio_holdings",
                Description: "Retrieve current portfolio holdings for an account",
                Parameters: new Dictionary<string, object>
                {
                    ["accountId"] = new { type = "integer", description = "Account ID" },
                    ["date"] = new { type = "string", description = "Date in YYYY-MM-DD format", optional = true }
                },
                Category: "Portfolio Data"
            ),
            new AiToolDto(
                Name: "analyze_portfolio_performance",
                Description: "Analyze portfolio performance and generate insights",
                Parameters: new Dictionary<string, object>
                {
                    ["accountId"] = new { type = "integer", description = "Account ID" },
                    ["analysisDate"] = new { type = "string", description = "Analysis date in YYYY-MM-DD format" }
                },
                Category: "Portfolio Analysis"
            ),
            new AiToolDto(
                Name: "get_market_context",
                Description: "Get market context and news for portfolio holdings",
                Parameters: new Dictionary<string, object>
                {
                    ["tickers"] = new { type = "array", description = "List of stock tickers" },
                    ["date"] = new { type = "string", description = "Date for market analysis" }
                },
                Category: "Market Intelligence"
            )
        };
    }

    private string DetermineQueryType(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("performance") || lowerQuery.Contains("doing") || lowerQuery.Contains("today"))
            return "Performance";
        if (lowerQuery.Contains("holdings") || lowerQuery.Contains("positions") || lowerQuery.Contains("stock"))
            return "Holdings";
        if (lowerQuery.Contains("market") || lowerQuery.Contains("news") || lowerQuery.Contains("sentiment"))
            return "Market";
        if (lowerQuery.Contains("risk") || lowerQuery.Contains("volatility") || lowerQuery.Contains("diversification"))
            return "Risk";
        if (lowerQuery.Contains("compare") || lowerQuery.Contains("between") || lowerQuery.Contains("vs"))
            return "Comparison";

        return "General";
    }

    private DateTime DetermineAnalysisDate(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // Check for specific dates mentioned in query
        if (lowerQuery.Contains("october 17") || lowerQuery.Contains("17th october") || lowerQuery.Contains("oct 17"))
            return new DateTime(2025, 10, 17);
        
        if (lowerQuery.Contains("october 18") || lowerQuery.Contains("18th october") || lowerQuery.Contains("oct 18"))
            return new DateTime(2025, 10, 18);
        
        if (lowerQuery.Contains("yesterday"))
            return DateTime.UtcNow.AddDays(-1);
        
        if (lowerQuery.Contains("today"))
            return DateTime.UtcNow;

        // Default to October 17, 2025 since that's a trading day when you have data
        return new DateTime(2025, 10, 17);
    }

    private async Task<string> GenerateResponseAsync(string query, string queryType, PortfolioAnalysisDto analysis, DateTime analysisDate, CancellationToken cancellationToken)
    {
        return queryType switch
        {
            "Performance" => await GeneratePerformanceResponse(query, analysis, analysisDate),
            "Holdings" => await GenerateHoldingsResponse(analysis, analysisDate),
            "Market" => await GenerateMarketResponse(analysis, analysisDate),
            "Risk" => await GenerateRiskResponse(analysis),
            "Comparison" => await GenerateComparisonResponse(analysis, analysisDate),
            _ => await GenerateGeneralResponse(analysis, analysisDate)
        };
    }

    private async Task<string> GeneratePerformanceResponse(string query, PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        var dateDisplay = analysisDate.ToString("MMMM dd, yyyy");
        var changeIcon = analysis.DayChangePercentage >= 0 ? "📈" : "📉";
        var changeColor = analysis.DayChangePercentage >= 0 ? "positive" : "negative";

        var response = $"📊 **Portfolio Performance for {dateDisplay}**\n\n";
        response += $"💰 **Total Value:** ${analysis.TotalValue:N2}\n";
        response += $"{changeIcon} **Daily Change:** ${analysis.DayChange:N2} ({analysis.DayChangePercentage:P2})\n";
        response += $"📋 **Holdings:** {analysis.HoldingPerformance.Count()} positions\n\n";

        if (analysis.Metrics.TopPerformers.Any())
        {
            response += $"🌟 **Top Performers:** {string.Join(", ", analysis.Metrics.TopPerformers)}\n";
        }

        if (analysis.Metrics.BottomPerformers.Any())
        {
            response += $"⚠️ **Underperformers:** {string.Join(", ", analysis.Metrics.BottomPerformers)}\n";
        }

        response += $"\n🎯 **Risk Profile:** {analysis.Metrics.RiskProfile}";

        return response;
    }

    private async Task<string> GenerateHoldingsResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        var response = $"📋 **Your Portfolio Holdings - {analysisDate:MMMM dd, yyyy}**\n\n";
        
        foreach (var holding in analysis.HoldingPerformance.OrderByDescending(h => h.CurrentValue))
        {
            var changeIcon = holding.DayChangePercentage >= 0 ? "📈" : "📉";
            response += $"• **{holding.InstrumentName}** ({holding.Ticker})\n";
            response += $"  💵 Value: ${holding.CurrentValue:N2} | Units: {holding.UnitAmount:N2}\n";
            response += $"  {changeIcon} Daily: ${holding.DayChange:N2} ({holding.DayChangePercentage:P2})\n";
            response += $"  📊 Total Return: ${holding.TotalReturn:N2} ({holding.TotalReturnPercentage:P2})\n\n";
        }

        return response;
    }

    private async Task<string> GenerateMarketResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        // Get tickers for market analysis
        var tickers = analysis.HoldingPerformance.Select(h => h.Ticker).ToList();
        
        try
        {
            var marketContext = await _marketIntelligenceService.GetMarketContextAsync(tickers, analysisDate);
            
            var response = $"📈 **Market Context for {analysisDate:MMMM dd, yyyy}**\n\n";
            response += $"🌍 **Market Summary:** {marketContext.MarketSummary}\n\n";
            response += $"😊 **Sentiment:** {marketContext.Sentiment.SentimentLabel} (Score: {marketContext.Sentiment.OverallSentimentScore:N2})\n";
            response += $"📊 **Fear & Greed Index:** {marketContext.Sentiment.FearGreedIndex}\n\n";

            if (marketContext.RelevantNews.Any())
            {
                response += "📰 **Recent News:**\n";
                foreach (var news in marketContext.RelevantNews.Take(3))
                {
                    response += $"• {news.Title} ({news.Source})\n";
                }
            }

            return response;
        }
        catch
        {
            return "📈 Market analysis is temporarily unavailable. Please try again later.";
        }
    }

    private async Task<string> GenerateRiskResponse(PortfolioAnalysisDto analysis)
    {
        await Task.CompletedTask;

        var response = $"⚖️ **Portfolio Risk Analysis**\n\n";
        response += $"🎯 **Risk Profile:** {analysis.Metrics.RiskProfile}\n";
        response += $"📊 **Daily Volatility:** {analysis.Metrics.DailyVolatility:P2}\n\n";

        // Concentration analysis
        var totalValue = analysis.TotalValue;
        var concentrationRisks = analysis.HoldingPerformance
            .Where(h => h.CurrentValue / totalValue > 0.25m)
            .ToList();

        if (concentrationRisks.Any())
        {
            response += "⚠️ **Concentration Risks:**\n";
            foreach (var risk in concentrationRisks)
            {
                var percentage = (risk.CurrentValue / totalValue) * 100;
                response += $"• {risk.Ticker}: {percentage:N1}% of portfolio\n";
            }
            response += "\n💡 Consider diversifying positions above 25% allocation.\n";
        }
        else
        {
            response += "✅ **Good diversification** - No single position exceeds 25% allocation.\n";
        }

        return response;
    }

    private async Task<string> GenerateComparisonResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        await Task.CompletedTask;

        try
        {
            var previousDate = analysisDate.AddDays(-1);
            var comparison = await _portfolioAnalysisService.ComparePerformanceAsync(
                analysis.AccountId, previousDate, analysisDate);

            var response = $"📊 **Portfolio Comparison: {previousDate:MMM dd} vs {analysisDate:MMM dd}**\n\n";
            response += $"💰 **Value Change:** ${comparison.StartValue:N2} → ${comparison.EndValue:N2}\n";
            response += $"📈 **Net Change:** ${comparison.TotalChange:N2} ({comparison.TotalChangePercentage:P2})\n\n";
            response += $"📋 **Trend:** {comparison.Insights.OverallTrend}\n";

            if (comparison.Insights.KeyDrivers.Any())
            {
                response += $"\n🔑 **Key Drivers:**\n";
                foreach (var driver in comparison.Insights.KeyDrivers.Take(3))
                {
                    response += $"• {driver}\n";
                }
            }

            return response;
        }
        catch
        {
            return "📊 Portfolio comparison is not available for the requested dates.";
        }
    }

    private async Task<string> GenerateGeneralResponse(PortfolioAnalysisDto analysis, DateTime analysisDate)
    {
        // Default to performance response for general queries
        return await GeneratePerformanceResponse("general", analysis, analysisDate);
    }

    private IEnumerable<InsightDto> GenerateInsights(PortfolioAnalysisDto analysis)
    {
        var insights = new List<InsightDto>();

        // Performance insights
        if (analysis.DayChangePercentage > 0.02m) // > 2%
        {
            insights.Add(new InsightDto(
                Type: "Performance",
                Title: "Strong Daily Performance",
                Description: $"Your portfolio gained {analysis.DayChangePercentage:P2} today, outperforming typical market movements.",
                Severity: "Positive"
            ));
        }
        else if (analysis.DayChangePercentage < -0.02m) // < -2%
        {
            insights.Add(new InsightDto(
                Type: "Performance", 
                Title: "Notable Daily Decline",
                Description: $"Your portfolio declined {Math.Abs(analysis.DayChangePercentage):P2} today. Consider reviewing individual holdings.",
                Severity: "Warning"
            ));
        }

        // Concentration insights
        var topHolding = analysis.HoldingPerformance.OrderByDescending(h => h.CurrentValue).FirstOrDefault();
        if (topHolding != null && (topHolding.CurrentValue / analysis.TotalValue) > 0.3m)
        {
            insights.Add(new InsightDto(
                Type: "Risk",
                Title: "High Concentration Risk",
                Description: $"{topHolding.Ticker} represents a large portion of your portfolio. Consider diversification.",
                Severity: "Warning",
                RelatedTickers: new[] { topHolding.Ticker }
            ));
        }

        return insights;
    }
}