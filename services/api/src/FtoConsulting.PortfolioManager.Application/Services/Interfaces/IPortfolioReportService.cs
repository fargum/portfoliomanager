using FtoConsulting.PortfolioManager.Application.DTOs;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for generating AI-powered portfolio reports and sending them via email
/// </summary>
public interface IPortfolioReportService
{
    /// <summary>
    /// Generate an AI portfolio report and send it to the configured recipient
    /// </summary>
    /// <param name="reportType">Morning (market open) or Evening (market close)</param>
    /// <param name="accountId">Account ID to report on</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report result including HTML content and email send status</returns>
    Task<PortfolioReportDto> GenerateAndSendReportAsync(
        ReportType reportType,
        int accountId,
        CancellationToken cancellationToken = default);
}
