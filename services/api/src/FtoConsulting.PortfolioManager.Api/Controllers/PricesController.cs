using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Price fetching and market data operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "RequirePortfolioScope")]
public class PricesController : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Prices");
    
    private readonly IPriceFetching _priceFetching;
    private readonly IPortfolioMappingService _mappingService;
    private readonly ILogger<PricesController> _logger;

    /// <summary>
    /// Initializes a new instance of the PricesController
    /// </summary>
    public PricesController(
        IPriceFetching priceFetching,
        IPortfolioMappingService mappingService,
        ILogger<PricesController> logger)
    {
        _priceFetching = priceFetching ?? throw new ArgumentNullException(nameof(priceFetching));
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetch market prices for all distinct ISINs from holdings for a specific valuation date
    /// </summary>
    /// <param name="valuationDate">The valuation date to fetch prices for (YYYY-MM-DD format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of price fetch operation with counts and metadata</returns>
    /// <remarks>
    /// This endpoint performs the following operations:
    /// 1. Queries all distinct tickers from holdings across all portfolios (regardless of holding date)
    /// 2. Uses EOD Historical Data API to fetch market prices for those tickers on the specified valuation date
    /// 3. Persists the pricing data to the instrument_prices table
    /// 4. Returns a summary of the operation (not the actual price data)
    /// 
    /// **Process Flow:**
    /// - Retrieves distinct tickers from all portfolio holdings in the database
    /// - Sends those tickers to the EOD Historical Data pricing engine for the specified valuation date
    /// - Persists market prices, currency, and market status information to database
    /// - Returns operation summary with success/failure counts and timing
    /// 
    /// **Example Usage:**
    /// ```
    /// GET /api/prices/date/2025-10-02
    /// ```
    /// 
    /// **Response Features:**
    /// - Operation summary with success/failure counts
    /// - Fetch performance metrics and timing information
    /// - Error details for ISINs that couldn't be priced
    /// - Data is persisted to database, not returned in response
    /// </remarks>
    /// <response code="200">Returns operation summary after persisting prices to database</response>
    /// <response code="400">Invalid date format</response>
    /// <response code="404">No instruments found in holdings database</response>
    /// <response code="500">Internal server error occurred while fetching prices</response>
    [HttpGet("date/{valuationDate:datetime}")]
    [ProducesResponseType(typeof(PricesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<PricesResponse>> FetchPricesForDate(
        [FromRoute] DateTime valuationDate,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("FetchPricesForDate");
        activity?.SetTag("valuation.date", valuationDate.ToString("yyyy-MM-dd"));
        
        try
        {
            using (_logger.BeginScope("Price fetching on {ValuationDate}", valuationDate))
            {
                _logger.LogInformation("Fetching market prices for holdings on ValuationDate={ValuationDate}",
                    valuationDate);
            }
            _logger.LogInformation("Fetching market prices for holdings on date {ValuationDate}", valuationDate);

            // Convert DateTime to DateOnly for business logic
            var dateOnly = DateOnly.FromDateTime(valuationDate);

            // Fetch and persist prices using application service
            var pricesResult = await _priceFetching.FetchAndPersistPricesForDateAsync(dateOnly, cancellationToken);

            // Check if any prices were successfully fetched and persisted
            if (pricesResult == null || pricesResult.SuccessfulPrices == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "No prices available");
                activity?.SetTag("error.type", "not_found");
                activity?.SetTag("error.reason", "no_prices");
                activity?.SetTag("prices.successful", "0");
                activity?.SetTag("prices.failed", (pricesResult?.FailedPrices ?? 0).ToString());
                
                _logger.LogWarning("No prices could be fetched for valuation date {ValuationDate}. Failed ISINs: {FailedCount}", 
                    dateOnly, pricesResult?.FailedPrices ?? 0);
                
                return NotFound(new ProblemDetails
                {
                    Title = "No Prices Available",
                    Detail = $"No market prices could be fetched for the specified valuation date: {dateOnly:yyyy-MM-dd}. " +
                            $"Total failures: {pricesResult?.FailedPrices ?? 0}. " +
                            $"This could be due to market closure, invalid Tickers, or external API issues.",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            // Return success response with operation summary (no actual price data)
            var response = new
            {
                ValuationDate = dateOnly,
                TotalInstruments = pricesResult.TotalTickers,
                SuccessfulPrices = pricesResult.SuccessfulPrices,
                FailedPrices = pricesResult.FailedPrices,
                FetchDurationMs = (long)pricesResult.FetchDuration.TotalMilliseconds,
                FetchedAt = pricesResult.FetchedAt,
                Message = $"Successfully fetched and persisted {pricesResult.SuccessfulPrices} prices to database",
                FailedTickers = pricesResult.FailedTickers
            };

            activity?.SetTag("prices.total_instruments", pricesResult.TotalTickers.ToString());
            activity?.SetTag("prices.successful", pricesResult.SuccessfulPrices.ToString());
            activity?.SetTag("prices.failed", pricesResult.FailedPrices.ToString());
            activity?.SetTag("prices.fetch_duration_ms", ((long)pricesResult.FetchDuration.TotalMilliseconds).ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("Successfully persisted {SuccessCount} prices for date {ValuationDate}", 
                pricesResult.SuccessfulPrices, dateOnly);

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
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            _logger.LogError(ex, "Error fetching prices for date {ValuationDate}", valuationDate);
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while fetching market prices",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }
}
