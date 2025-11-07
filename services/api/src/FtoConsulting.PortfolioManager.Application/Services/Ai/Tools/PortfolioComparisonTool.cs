using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Utilities;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

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
        [Description("Start date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string startDate,
        [Description("End date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string endDate,
        CancellationToken cancellationToken = default)
    {
        var parsedStartDate = DateUtilities.ParseDateTime(startDate);
        var parsedEndDate = DateUtilities.ParseDateTime(endDate);
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