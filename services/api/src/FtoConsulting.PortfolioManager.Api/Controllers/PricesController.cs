using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Price fetching and market data operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PricesController : ControllerBase
{
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
    /// Fetch current market prices for all distinct ISINs from holdings on a specific valuation date
    /// </summary>
    /// <param name="valuationDate">The valuation date to retrieve holdings and fetch prices for (YYYY-MM-DD format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of current market prices for all distinct ISINs found in holdings</returns>
    /// <remarks>
    /// This endpoint performs the following operations:
    /// 1. Queries all distinct ISINs from holdings across all portfolios for the specified date
    /// 2. Uses EOD Historical Data API to fetch current market prices for those ISINs
    /// 3. Returns the pricing data with metadata about the fetch operation
    /// 
    /// **Process Flow:**
    /// - Retrieves distinct ISINs from all portfolio holdings on the specified date
    /// - Sends those ISINs to the EOD Historical Data pricing engine
    /// - Returns current market prices, currency, and market status information
    /// - Includes fetch timestamp and success/failure indicators
    /// 
    /// **Example Usage:**
    /// ```
    /// GET /api/prices/date/2025-10-02
    /// ```
    /// 
    /// **Response Features:**
    /// - Real-time or latest available market prices
    /// - Currency information for each instrument
    /// - Market status (open/closed) and trading information  
    /// - Error handling for ISINs that couldn't be priced
    /// - Fetch performance metrics and timing information
    /// </remarks>
    /// <response code="200">Returns the collection of market prices for ISINs found on the specified date</response>
    /// <response code="400">Invalid date format</response>
    /// <response code="404">No holdings found for the specified date</response>
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
        try
        {
            _logger.LogInformation("Fetching market prices for holdings on date {ValuationDate}", valuationDate);

            // Convert DateTime to DateOnly for business logic
            var dateOnly = DateOnly.FromDateTime(valuationDate);

            // Fetch prices using application service (which will internally get ISINs and fetch prices)
            var pricesResult = await _priceFetching.FetchPricesForDateAsync(dateOnly, cancellationToken);

            // Check if any prices were fetched
            if (pricesResult == null || !pricesResult.Prices.Any())
            {
                _logger.LogWarning("No prices could be fetched for date {ValuationDate}", dateOnly);
                return NotFound(new ProblemDetails
                {
                    Title = "No Prices Available",
                    Detail = $"No market prices could be fetched for holdings on the specified date: {dateOnly:yyyy-MM-dd}",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            // Map to response DTO
            var response = _mappingService.MapToPricesResponse(pricesResult, dateOnly);

            _logger.LogInformation("Successfully fetched {SuccessCount} prices out of {TotalCount} ISINs for date {ValuationDate}", 
                pricesResult.SuccessfulPrices, pricesResult.TotalIsins, dateOnly);

            return Ok(response);
        }
        catch (FormatException ex)
        {
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