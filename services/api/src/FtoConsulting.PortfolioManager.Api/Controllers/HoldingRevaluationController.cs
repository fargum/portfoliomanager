using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Controller for holding revaluation operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HoldingRevaluationController : ControllerBase
{
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
        try
        {
            // Parse the valuation date
            if (!DateOnly.TryParseExact(valuationDate, "yyyy-MM-dd", out var parsedDate))
            {
                _logger.LogWarning("Invalid valuation date format provided: {ValuationDate}", valuationDate);
                return BadRequest($"Invalid valuation date format. Expected format: YYYY-MM-DD, received: {valuationDate}");
            }

            _logger.LogInformation("Starting holding revaluation for date {ValuationDate}", parsedDate);

            var result = await _holdingRevaluationService.RevalueHoldingsAsync(parsedDate, cancellationToken);

            _logger.LogInformation("Holding revaluation completed for {ValuationDate}. Success: {Success}, Failed: {Failed}", 
                parsedDate, result.SuccessfulRevaluations, result.FailedRevaluations);

            return Ok(result);
        }
        catch (Exception ex)
        {
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
}