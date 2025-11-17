using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Utilities;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

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

    [Description("Analyze portfolio performance and generate insights for a specific date. For current/today performance, use today's date to get real-time analysis.")]
    public async Task<object> AnalyzePortfolioPerformance(
        [Description("Account ID")] int accountId,
        [Description("Analysis date. Use 'today' or current date (YYYY-MM-DD) for real-time analysis, or specify historical date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string analysisDate,
        CancellationToken cancellationToken = default)
    {
        // Smart date handling: if asking for 'today', 'current', or similar, use today's date
        var effectiveDate = analysisDate;
        if (string.IsNullOrEmpty(analysisDate) || 
            analysisDate.ToLowerInvariant().Contains("today") || 
            analysisDate.ToLowerInvariant().Contains("current") ||
            analysisDate.ToLowerInvariant().Contains("now"))
        {
            effectiveDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        }
        
        var parsedDate = DateUtilities.ParseDateTime(effectiveDate);
        var analysis = await _portfolioAnalysisService.AnalyzePortfolioPerformanceAsync(accountId, parsedDate, cancellationToken);
        
        return new
        {
            AccountId = accountId,
            RequestedDate = analysisDate,
            EffectiveDate = effectiveDate,
            IsRealTimeAnalysis = parsedDate.Date == DateTime.UtcNow.Date,
            Analysis = analysis
        };
    }
}