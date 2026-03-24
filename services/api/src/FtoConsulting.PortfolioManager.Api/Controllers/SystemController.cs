using FtoConsulting.PortfolioManager.Application.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Api.Authentication;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// System endpoints for scheduled jobs and internal service calls.
/// Uses API key authentication instead of user OAuth.
/// </summary>
[ApiController]
[Route("api/system")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = SystemApiKeyAuthenticationHandler.SchemeName)]
[ApiExplorerSettings(IgnoreApi = true)] // Hide from Swagger - internal endpoints
public class SystemController(
    IHoldingRevaluationService holdingRevaluationService,
    IPortfolioReportService portfolioReportService,
    ILogger<SystemController> logger) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.System");

    /// <summary>
    /// System endpoint for scheduled jobs (e.g., Azure Functions) to trigger automated revaluation.
    /// Uses API key authentication instead of user authentication.
    /// Automatically calculates the previous business day if no date is provided.
    /// </summary>
    /// <param name="valuationDate">Optional date to revalue (format: YYYY-MM-DD). Defaults to previous business day.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined result with both price fetch and revaluation statistics</returns>
    /// <response code="200">Automated revaluation completed successfully</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error during revaluation</response>
    [HttpPost("automated-revaluation")]
    [ProducesResponseType(typeof(Application.Models.CombinedPriceAndRevaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Application.Models.CombinedPriceAndRevaluationResult>> AutomatedRevaluation(
        [FromQuery] string? valuationDate = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("SystemAutomatedRevaluation");
        activity?.SetTag("operation.type", "system_automated");
        activity?.SetTag("trigger", "scheduled_job");
        
        try
        {
            // Calculate target date: use provided date or default to previous business day
            DateOnly targetDate;
            if (!string.IsNullOrEmpty(valuationDate))
            {
                try
                {
                    targetDate = DateUtilities.ParseDate(valuationDate);
                }
                catch (Exception parseEx)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, parseEx.Message);
                    logger.LogWarning(parseEx, "Invalid valuation date format provided to system endpoint: {ValuationDate}", valuationDate);
                    return BadRequest($"Invalid valuation date format: {valuationDate}");
                }
            }
            else
            {
                // Default to previous business day (skip weekends)
                targetDate = GetPreviousBusinessDay(DateOnly.FromDateTime(DateTime.UtcNow));
            }

            activity?.SetTag("valuation.date", targetDate.ToString("yyyy-MM-dd"));
            activity?.SetTag("valuation.date_source", string.IsNullOrEmpty(valuationDate) ? "calculated" : "provided");

            logger.LogInformation("System automated revaluation triggered for date {TargetDate} (source: {Source})",
                targetDate, string.IsNullOrEmpty(valuationDate) ? "calculated previous business day" : "provided");

            var result = await holdingRevaluationService.FetchPricesAndRevalueHoldingsAsync(targetDate, cancellationToken);

            activity?.SetTag("prices.successful", result.PriceFetchResult.SuccessfulPrices.ToString());
            activity?.SetTag("prices.failed", result.PriceFetchResult.FailedPrices.ToString());
            activity?.SetTag("revaluation.successful", result.HoldingRevaluationResult.SuccessfulRevaluations.ToString());
            activity?.SetTag("revaluation.failed", result.HoldingRevaluationResult.FailedRevaluations.ToString());
            activity?.SetTag("combined.success", result.OverallSuccess.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            logger.LogInformation("System automated revaluation completed for {TargetDate}. {Summary}", 
                targetDate, result.Summary);

            return Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error during system automated revaluation");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred during the automated revaluation process.");
        }
    }

    /// <summary>
    /// System endpoint for scheduled jobs to trigger AI portfolio report generation and email delivery.
    /// Generates a morning or evening report for the specified account and sends it via email.
    /// </summary>
    /// <param name="reportType">Report type: "morning" or "evening"</param>
    /// <param name="accountId">Account ID to generate the report for (defaults to 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report result including AI narrative, HTML content, and email send status</returns>
    /// <response code="200">Report generated (and email attempted)</response>
    /// <response code="400">Invalid report type</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error during report generation</response>
    [HttpPost("portfolio-report")]
    [ProducesResponseType(typeof(PortfolioReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PortfolioReportDto>> PortfolioReport(
        [FromQuery] string reportType = "morning",
        [FromQuery] int accountId = 1,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("SystemPortfolioReport");
        activity?.SetTag("report.type", reportType);
        activity?.SetTag("account.id", accountId.ToString());

        if (!Enum.TryParse<ReportType>(reportType, ignoreCase: true, out var parsedReportType))
        {
            return BadRequest($"Invalid reportType '{reportType}'. Valid values are 'morning' or 'evening'.");
        }

        try
        {
            logger.LogInformation("System portfolio report triggered: type={ReportType}, accountId={AccountId}",
                reportType, accountId);

            var result = await portfolioReportService.GenerateAndSendReportAsync(
                parsedReportType, accountId, cancellationToken);

            activity?.SetTag("report.email_sent", result.EmailSent.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            logger.LogInformation(
                "Portfolio report completed: type={ReportType}, accountId={AccountId}, emailSent={EmailSent}",
                reportType, accountId, result.EmailSent);

            return Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error generating portfolio report: type={ReportType}, accountId={AccountId}",
                reportType, accountId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "An error occurred during portfolio report generation.");
        }
    }

    /// <summary>
    /// Health check endpoint for system monitoring (no authentication required)
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Gets the previous business day, skipping weekends
    /// </summary>
    private static DateOnly GetPreviousBusinessDay(DateOnly date)
    {
        var previousDay = date.AddDays(-1);
        
        // Skip weekends
        while (previousDay.DayOfWeek == DayOfWeek.Saturday || previousDay.DayOfWeek == DayOfWeek.Sunday)
        {
            previousDay = previousDay.AddDays(-1);
        }
        
        return previousDay;
    }
}
