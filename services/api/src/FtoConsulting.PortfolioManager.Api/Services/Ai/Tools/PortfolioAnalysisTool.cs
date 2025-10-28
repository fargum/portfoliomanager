using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services.Ai;

namespace FtoConsulting.PortfolioManager.Api.Services.Ai.Tools;

/// <summary>
/// MCP tool for portfolio performance analysis
/// </summary>
public class PortfolioAnalysisTool
{
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;

    public PortfolioAnalysisTool(IPortfolioAnalysisService portfolioAnalysisService)
    {
        _portfolioAnalysisService = portfolioAnalysisService;
    }

    [Description("Analyze portfolio performance and generate insights for a specific date")]
    public async Task<object> AnalyzePortfolioPerformance(
        [Description("Account ID")] int accountId,
        [Description("Analysis date in YYYY-MM-DD format")] string analysisDate,
        CancellationToken cancellationToken = default)
    {
        var parsedDate = DateTime.Parse(analysisDate);
        var analysis = await _portfolioAnalysisService.AnalyzePortfolioPerformanceAsync(accountId, parsedDate, cancellationToken);
        
        return new
        {
            AccountId = accountId,
            AnalysisDate = analysisDate,
            Analysis = analysis
        };
    }
}