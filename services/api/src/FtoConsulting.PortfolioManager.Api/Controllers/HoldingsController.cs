using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Holdings retrieval and analysis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HoldingsController : ControllerBase
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;
    private readonly IPortfolioMappingService _mappingService;
    private readonly ILogger<HoldingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the HoldingsController
    /// </summary>
    public HoldingsController(
        IHoldingsRetrieval holdingsRetrieval,
        IPortfolioMappingService mappingService,
        ILogger<HoldingsController> logger)
    {
        _holdingsRetrieval = holdingsRetrieval;
        _mappingService = mappingService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve all holdings for a specific account and valuation date
    /// </summary>
    /// <param name="accountId">The unique identifier of the account</param>
    /// <param name="valuationDate">The valuation date to retrieve holdings for (YYYY-MM-DD format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of flattened holdings data including portfolio, instrument, and platform information</returns>
    /// <remarks>
    /// This endpoint retrieves all holdings across all portfolios for a given account on a specific valuation date.
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
    /// GET /api/holdings/account/12345678-1234-5678-9012-123456789012/date/2025-09-27
    /// ```
    /// 
    /// **Response Features:**
    /// - All monetary values are returned as decimals with appropriate precision
    /// - Gain/loss percentages are calculated and rounded to 2 decimal places
    /// - Holdings are ordered by portfolio name, then by instrument name
    /// - All related entity data is included to minimize additional API calls
    /// </remarks>
    /// <response code="200">Returns the collection of holdings for the specified account and date</response>
    /// <response code="400">Invalid account ID format or date format</response>
    /// <response code="404">No holdings found for the specified account and date</response>
    /// <response code="500">Internal server error occurred while retrieving holdings</response>
    [HttpGet("account/{accountId:guid}/date/{valuationDate:datetime}")]
    [ProducesResponseType(typeof(AccountHoldingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<AccountHoldingsResponse>> GetHoldingsByAccountAndDate(
        [FromRoute] Guid accountId,
        [FromRoute] DateTime valuationDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);

            // Validate input parameters
            if (accountId == Guid.Empty)
            {
                _logger.LogWarning("Invalid account ID provided: {AccountId}", accountId);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Account ID",
                    Detail = "Account ID cannot be empty",
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            // Convert DateTime to DateOnly for service call
            var dateOnly = DateOnly.FromDateTime(valuationDate);

            // Retrieve holdings from the service
            var holdings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(accountId, dateOnly, cancellationToken);

            // Check if any holdings were found
            if (!holdings.Any())
            {
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

            _logger.LogInformation("Successfully retrieved {Count} holdings for account {AccountId} on date {ValuationDate}", 
                response.TotalHoldings, accountId, dateOnly);

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