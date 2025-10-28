using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services.Ai;

namespace FtoConsulting.PortfolioManager.Api.Services.Ai.Tools;

/// <summary>
/// MCP tool for portfolio performance comparison
/// </summary>
public class PortfolioComparisonTool
{
    private readonly IPortfolioAnalysisService _portfolioAnalysisService;

    public PortfolioComparisonTool(IPortfolioAnalysisService portfolioAnalysisService)
    {
        _portfolioAnalysisService = portfolioAnalysisService;
    }

    [Description("Compare portfolio performance between two dates")]
    public async Task<object> ComparePortfolioPerformance(
        [Description("Account ID")] int accountId,
        [Description("Start date in YYYY-MM-DD format")] string startDate,
        [Description("End date in YYYY-MM-DD format")] string endDate,
        CancellationToken cancellationToken = default)
    {
        var parsedStartDate = DateTime.Parse(startDate);
        var parsedEndDate = DateTime.Parse(endDate);
        var comparison = await _portfolioAnalysisService.ComparePerformanceAsync(accountId, parsedStartDate, parsedEndDate, cancellationToken);
        
        return new
        {
            AccountId = accountId,
            StartDate = startDate,
            EndDate = endDate,
            Comparison = comparison
        };
    }
}