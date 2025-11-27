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
[Authorize(Policy = "RequirePortfolioScope")] 
public class HoldingRevaluationController(
    IHoldingRevaluationService holdingRevaluationService,
    ILogger<HoldingRevaluationController> logger) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Revaluation");

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
            using (logger.BeginScope("Price fetch and revaluation on {ValuationDate}", valuationDate))
            {
                logger.LogInformation("Starting price fetch and holdings revaluation for ValuationDate={ValuationDate}",
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
                
                logger.LogWarning(parseEx, "Invalid valuation date format provided: {ValuationDate}", valuationDate);
                return BadRequest($"Invalid valuation date format. Expected formats include YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, received: {valuationDate}");
            }

            logger.LogInformation("Starting combined price fetch and holding revaluation for date {ValuationDate}", parsedDate);

            var result = await holdingRevaluationService.FetchPricesAndRevalueHoldingsAsync(parsedDate, cancellationToken);

            activity?.SetTag("prices.successful", result.PriceFetchResult.SuccessfulPrices.ToString());
            activity?.SetTag("prices.failed", result.PriceFetchResult.FailedPrices.ToString());
            activity?.SetTag("revaluation.successful", result.HoldingRevaluationResult.SuccessfulRevaluations.ToString());
            activity?.SetTag("revaluation.failed", result.HoldingRevaluationResult.FailedRevaluations.ToString());
            activity?.SetTag("combined.success", result.OverallSuccess.ToString());
            activity?.SetTag("combined.total_duration_ms", result.TotalDuration.TotalMilliseconds.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            logger.LogInformation("Combined operation completed for {ValuationDate}. {Summary}", 
                parsedDate, result.Summary);

            return Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            activity?.SetTag("operation.type", "combined");
            
            logger.LogError(ex, "Error during combined price fetch and revaluation for date {ValuationDate}", valuationDate);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred during the combined price fetch and revaluation process. Please check the logs for details.");
        }
    }
}
