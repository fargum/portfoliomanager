using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Controller for holding revaluation operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// [Authorize(Policy = "RequirePortfolioScope")] // Temporarily disabled for scripting
public class HoldingRevaluationController : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Revaluation");
    
    private readonly IHoldingRevaluationService _holdingRevaluationService;
    private readonly ILogger<HoldingRevaluationController> _logger;

    public HoldingRevaluationController(
        IHoldingRevaluationService holdingRevaluationService,
        ILogger<HoldingRevaluationController> logger)
    {
        _holdingRevaluationService = holdingRevaluationService ?? throw new ArgumentNullException(nameof(holdingRevaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Revalues all holdings for a specific valuation date using current market prices
    /// </summary>
    /// <param name="valuationDate">The date to revalue holdings for (format: YYYY-MM-DD)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Revaluation result with statistics</returns>
    /// <response code="200">Revaluation completed successfully</response>
    /// <response code="400">Invalid valuation date format</response>
    /// <response code="500">Internal server error during revaluation</response>
    [HttpPost("revalue/{valuationDate}")]
    [ProducesResponseType(typeof(Application.Models.HoldingRevaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Application.Models.HoldingRevaluationResult>> RevalueHoldings(
        string valuationDate,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("RevalueHoldings");
        activity?.SetTag("valuation.date", valuationDate);
        
        try
        {
            using (_logger.BeginScope("Holdings revaluation on {ValuationDate}", valuationDate))
            {
                _logger.LogInformation("Starting holdings revaluation for ValuationDate={ValuationDate}",
                    valuationDate);
            }
            
            // Parse the valuation date using DateUtilities for consistent parsing
            DateOnly parsedDate;
            try
            {
                parsedDate = DateUtilities.ParseDate(valuationDate);
                activity?.SetTag("valuation.date_parsed", parsedDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception parseEx)
            {
                activity?.SetStatus(ActivityStatusCode.Error, parseEx.Message);
                activity?.SetTag("error.type", "parse");
                activity?.SetTag("error.reason", "invalid_date_format");
                
                _logger.LogWarning(parseEx, "Invalid valuation date format provided: {ValuationDate}", valuationDate);
                return BadRequest($"Invalid valuation date format. Expected formats include YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, received: {valuationDate}");
            }

            _logger.LogInformation("Starting holding revaluation for date {ValuationDate}", parsedDate);

            var result = await _holdingRevaluationService.RevalueHoldingsAsync(parsedDate, cancellationToken);

            activity?.SetTag("revaluation.successful", result.SuccessfulRevaluations.ToString());
            activity?.SetTag("revaluation.failed", result.FailedRevaluations.ToString());
            activity?.SetTag("revaluation.total", result.TotalHoldings.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("Holding revaluation completed for {ValuationDate}. Success: {Success}, Failed: {Failed}", 
                parsedDate, result.SuccessfulRevaluations, result.FailedRevaluations);

            return Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            _logger.LogError(ex, "Error during holding revaluation for date {ValuationDate}", valuationDate);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred during the revaluation process. Please check the logs for details.");
        }
    }

    /// <summary>
    /// Revalues holdings for today's date using current market prices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Revaluation result with statistics</returns>
    /// <response code="200">Revaluation completed successfully</response>
    /// <response code="500">Internal server error during revaluation</response>
    [HttpPost("revalue/today")]
    [ProducesResponseType(typeof(Application.Models.HoldingRevaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Application.Models.HoldingRevaluationResult>> RevalueHoldingsToday(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await RevalueHoldings(today.ToString("yyyy-MM-dd"), cancellationToken);
    }

    /// <summary>
    /// Fetches current market prices and then revalues all holdings for a specific valuation date
    /// This combined operation first fetches market prices from external data sources, 
    /// then uses those prices to revalue all holdings
    /// </summary>
    /// <param name="valuationDate">The date to fetch prices and revalue holdings for (format: YYYY-MM-DD)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined result with both price fetch and revaluation statistics</returns>
    /// <response code="200">Combined operation completed successfully</response>
    /// <response code="400">Invalid valuation date format</response>
    /// <response code="500">Internal server error during combined operation</response>
    [HttpPost("fetch-prices-and-revalue/{valuationDate}")]
    [ProducesResponseType(typeof(Application.Models.CombinedPriceAndRevaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Application.Models.CombinedPriceAndRevaluationResult>> FetchPricesAndRevalueHoldings(
        string valuationDate,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("FetchPricesAndRevalueHoldings");
        activity?.SetTag("valuation.date", valuationDate);
        activity?.SetTag("operation.type", "combined");
        
        try
        {
            using (_logger.BeginScope("Price fetch and revaluation on {ValuationDate}", valuationDate))
            {
                _logger.LogInformation("Starting price fetch and holdings revaluation for ValuationDate={ValuationDate}",
                    valuationDate);
            }
            
            // Parse the valuation date using DateUtilities for consistent parsing
            DateOnly parsedDate;
            try
            {
                parsedDate = DateUtilities.ParseDate(valuationDate);
                activity?.SetTag("valuation.date_parsed", parsedDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception parseEx)
            {
                activity?.SetStatus(ActivityStatusCode.Error, parseEx.Message);
                activity?.SetTag("error.type", "parse");
                activity?.SetTag("error.reason", "invalid_date_format");
                
                _logger.LogWarning(parseEx, "Invalid valuation date format provided: {ValuationDate}", valuationDate);
                return BadRequest($"Invalid valuation date format. Expected formats include YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, received: {valuationDate}");
            }

            _logger.LogInformation("Starting combined price fetch and holding revaluation for date {ValuationDate}", parsedDate);

            var result = await _holdingRevaluationService.FetchPricesAndRevalueHoldingsAsync(parsedDate, cancellationToken);

            activity?.SetTag("prices.successful", result.PriceFetchResult.SuccessfulPrices.ToString());
            activity?.SetTag("prices.failed", result.PriceFetchResult.FailedPrices.ToString());
            activity?.SetTag("revaluation.successful", result.HoldingRevaluationResult.SuccessfulRevaluations.ToString());
            activity?.SetTag("revaluation.failed", result.HoldingRevaluationResult.FailedRevaluations.ToString());
            activity?.SetTag("combined.success", result.OverallSuccess.ToString());
            activity?.SetTag("combined.total_duration_ms", result.TotalDuration.TotalMilliseconds.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("Combined operation completed for {ValuationDate}. {Summary}", 
                parsedDate, result.Summary);

            return Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            activity?.SetTag("operation.type", "combined");
            
            _logger.LogError(ex, "Error during combined price fetch and revaluation for date {ValuationDate}", valuationDate);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred during the combined price fetch and revaluation process. Please check the logs for details.");
        }
    }

    /// <summary>
    /// Fetches current market prices and then revalues holdings for today's date
    /// This combined operation first fetches market prices from external data sources, 
    /// then uses those prices to revalue all holdings for the current date
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined result with both price fetch and revaluation statistics</returns>
    /// <response code="200">Combined operation completed successfully</response>
    /// <response code="500">Internal server error during combined operation</response>
    [HttpPost("fetch-prices-and-revalue/today")]
    [ProducesResponseType(typeof(Application.Models.CombinedPriceAndRevaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Application.Models.CombinedPriceAndRevaluationResult>> FetchPricesAndRevalueHoldingsToday(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await FetchPricesAndRevalueHoldings(today.ToString("yyyy-MM-dd"), cancellationToken);
    }
}
