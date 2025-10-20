using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Service for analyzing portfolio performance and generating insights
/// </summary>
public interface IPortfolioAnalysisService
{
    /// <summary>
    /// Analyze portfolio performance for a specific account and date
    /// </summary>
    /// <param name="accountId">The account ID to analyze</param>
    /// <param name="analysisDate">The date to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Portfolio analysis results</returns>
    Task<PortfolioAnalysisDto> AnalyzePortfolioPerformanceAsync(int accountId, DateTime analysisDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare portfolio performance between two dates
    /// </summary>
    /// <param name="accountId">The account ID to analyze</param>
    /// <param name="startDate">Start date for comparison</param>
    /// <param name="endDate">End date for comparison</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Performance comparison results</returns>
    Task<PerformanceComparisonDto> ComparePerformanceAsync(int accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}