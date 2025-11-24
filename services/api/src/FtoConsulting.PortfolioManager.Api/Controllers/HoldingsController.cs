using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Holdings retrieval and analysis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "RequirePortfolioScope")]
public class HoldingsController : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Holdings");
    
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioMappingService _mappingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly MetricsService _metrics;
    private readonly ILogger<HoldingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the HoldingsController
    /// </summary>
    public HoldingsController(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioMappingService mappingService,
        ICurrentUserService currentUserService,
        MetricsService metrics,
        ILogger<HoldingsController> logger)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _mappingService = mappingService;
        _currentUserService = currentUserService;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve all holdings for the current authenticated user and valuation date
    /// </summary>
    /// <param name="valuationDate">The valuation date to retrieve holdings for (YYYY-MM-DD format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of flattened holdings data including portfolio, instrument, and platform information</returns>
    /// <remarks>
    /// This endpoint retrieves all holdings across all portfolios for the authenticated user on a specific valuation date.
    /// The account is automatically determined from the user's authentication token.
    /// 
    /// **Real-Time vs Historical Data:**
    /// - If the valuation date is today's date, holdings are returned with real-time market prices
    /// - If the valuation date is in the past, holdings are returned with historical data from the database
    /// 
    /// The response includes flattened data combining information from:
    /// 
    /// **Holdings Data:**
    /// - Unit amounts, bought values, current values
    /// - Calculated gain/loss amounts and percentages
    /// - Valuation date information
    /// 
    /// **Portfolio Data:**
    /// - Portfolio names and identifiers
    /// - Account information
    /// 
    /// **Instrument Data:**
    /// - ISIN, SEDOL identifiers
    /// - Instrument names, descriptions, and types
    /// 
    /// **Platform Data:**
    /// - Platform names where holdings are held
    /// 
    /// **Summary Information:**
    /// - Total holdings count
    /// - Aggregated current and bought values
    /// - Overall gain/loss calculations
    /// 
    /// **Example Usage:**
    /// ```
    /// GET /api/holdings/date/2025-11-10  // Returns real-time data for today
    /// GET /api/holdings/date/2025-09-27  // Returns historical data
    /// ```
    /// 
    /// **Response Features:**
    /// - All monetary values are returned as decimals with appropriate precision
    /// - Gain/loss percentages are calculated and rounded to 2 decimal places
    /// - Holdings are ordered by portfolio name, then by instrument name
    /// - All related entity data is included to minimize additional API calls
    /// - Real-time prices are fetched live and not persisted to the database
    /// </remarks>
    /// <response code="200">Returns the collection of holdings for the authenticated user and date</response>
    /// <response code="400">Invalid date format</response>
    /// <response code="404">No holdings found for the specified date</response>
    /// <response code="500">Internal server error occurred while retrieving holdings</response>
    [HttpGet("date/{valuationDate:datetime}")]
    [ProducesResponseType(typeof(AccountHoldingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<AccountHoldingsResponse>> GetHoldingsByDate(
        [FromRoute] DateTime valuationDate,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("GetHoldingsByDate");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Get account ID from authenticated user
        var accountId = await _currentUserService.GetCurrentUserAccountIdAsync();
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("valuation.date", valuationDate.ToString("yyyy-MM-dd"));
        activity?.SetTag("is.real_time", (valuationDate.Date == DateTime.Today).ToString());
        
        // Record request metric
        _metrics.IncrementHoldingsRequests(accountId.ToString(), "requested");
        
        try
        {
            using (_logger.BeginScope("Holdings retrieval for account {AccountId} on {ValuationDate}", accountId, valuationDate))
            {
                _logger.LogInformation("Processing holdings request with parameters: AccountId={AccountId}, ValuationDate={ValuationDate}, IsRealTime={IsRealTime}",
                    accountId, valuationDate, valuationDate.Date == DateTime.Today);
            }
            
            // Validate input parameters - no need to validate accountId since it's controlled by the system
            
            // Convert DateTime to DateOnly for service call
            var dateOnly = DateOnly.FromDateTime(valuationDate);
            _logger.LogDebug("Converted valuation date to {DateOnly} for service call", dateOnly);

            // Retrieve holdings from the service - it will handle both real-time and historical data automatically
            _logger.LogInformation("Calling holdings retrieval service for account {AccountId}", accountId);
            var holdings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(accountId, dateOnly, cancellationToken);

            // Check if any holdings were found
            if (!holdings.Any())
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Holdings not found");
                activity?.SetTag("error.type", "not_found");
                activity?.SetTag("error.reason", "no_holdings");
                activity?.SetTag("holdings.count", "0");
                
                _logger.LogInformation("No holdings found for account {AccountId} on date {ValuationDate}", accountId, dateOnly);
                return NotFound(new ProblemDetails
                {
                    Title = "Holdings Not Found",
                    Detail = $"No holdings found for account {accountId} on date {dateOnly:yyyy-MM-dd}",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            // Map to response DTO
            var response = _mappingService.MapToAccountHoldingsResponse(holdings, accountId, dateOnly);

            activity?.SetTag("holdings.count", response.TotalHoldings.ToString());
            activity?.SetTag("response.total_current_value", response.TotalCurrentValue.ToString());
            activity?.SetTag("response.total_gain_loss", response.TotalGainLoss.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Record metrics
            stopwatch.Stop();
            _metrics.RecordHoldingsRequestDuration(stopwatch.Elapsed.TotalSeconds, accountId.ToString(), "success");
            _metrics.IncrementHoldingsRequests(accountId.ToString(), "success");

            _logger.LogInformation("Successfully retrieved {Count} holdings for account {AccountId} on date {ValuationDate}", 
                response.TotalHoldings, accountId, dateOnly);

            return Ok(response);
        }
        catch (FormatException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "format");
            activity?.SetTag("error.reason", "invalid_date");
            
            _logger.LogWarning(ex, "Invalid date format provided: {ValuationDate}", valuationDate);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Date Format",
                Detail = "Date must be in YYYY-MM-DD format",
                Status = (int)HttpStatusCode.BadRequest
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordHoldingsRequestDuration(stopwatch.Elapsed.TotalSeconds, accountId.ToString(), "error");
            _metrics.IncrementHoldingsRequests(accountId.ToString(), "error");
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            _logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving holdings data",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }
}
