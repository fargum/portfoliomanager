using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Models.Requests;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public class HoldingsController(
    IHoldingService holdingService,
    IPortfolioMappingService mappingService,
    ICurrentUserService currentUserService,
    MetricsService metrics,
    ILogger<HoldingsController> logger) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Holdings");

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
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        logger.LogInformation("GetHoldingsByDate: Retrieved AccountId from authentication: {AccountId}", accountId);
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("valuation.date", valuationDate.ToString("yyyy-MM-dd"));
        activity?.SetTag("is.real_time", (valuationDate.Date == DateTime.Today).ToString());
        
        // Record request metric
        metrics.IncrementHoldingsRequests(accountId.ToString(), "requested");
        
        try
        {
            using (logger.BeginScope("Holdings retrieval for account {AccountId} on {ValuationDate}", accountId, valuationDate))
            {
                logger.LogInformation("Processing holdings request with parameters: AccountId={AccountId}, ValuationDate={ValuationDate}, IsRealTime={IsRealTime}",
                    accountId, valuationDate, valuationDate.Date == DateTime.Today);
            }
            
            // Convert DateTime to DateOnly for service call
            var dateOnly = DateOnly.FromDateTime(valuationDate);
            logger.LogDebug("Converted valuation date to {DateOnly} for service call", dateOnly);

            // Retrieve holdings from the service - it will handle both real-time and historical data automatically
            logger.LogInformation("Calling holdings retrieval service for account {AccountId}", accountId);
            var holdings = await holdingService.GetHoldingsByAccountAndDateAsync(accountId, dateOnly, cancellationToken);

            // Check if any holdings were found
            if (!holdings.Any())
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Holdings not found");
                activity?.SetTag("error.type", "not_found");
                activity?.SetTag("error.reason", "no_holdings");
                activity?.SetTag("holdings.count", "0");
                
                logger.LogInformation("No holdings found for account {AccountId} on date {ValuationDate}", accountId, dateOnly);
                return NotFound(new ProblemDetails
                {
                    Title = "Holdings Not Found",
                    Detail = $"No holdings found for account {accountId} on date {dateOnly:yyyy-MM-dd}",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            // Map to response DTO
            var response = mappingService.MapToAccountHoldingsResponse(holdings, accountId, dateOnly);

            activity?.SetTag("holdings.count", response.TotalHoldings.ToString());
            activity?.SetTag("response.total_current_value", response.TotalCurrentValue.ToString());
            activity?.SetTag("response.total_gain_loss", response.TotalGainLoss.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Record metrics
            stopwatch.Stop();
            metrics.RecordHoldingsRequestDuration(stopwatch.Elapsed.TotalSeconds, accountId.ToString(), "success");
            metrics.IncrementHoldingsRequests(accountId.ToString(), "success");

            logger.LogInformation("Successfully retrieved {Count} holdings for account {AccountId} on date {ValuationDate}", 
                response.TotalHoldings, accountId, dateOnly);

            return Ok(response);
        }
        catch (FormatException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "format");
            activity?.SetTag("error.reason", "invalid_date");
            
            logger.LogWarning(ex, "Invalid date format provided: {ValuationDate}", valuationDate);
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
            metrics.RecordHoldingsRequestDuration(stopwatch.Elapsed.TotalSeconds, accountId.ToString(), "error");
            metrics.IncrementHoldingsRequests(accountId.ToString(), "error");
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving holdings data",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    /// <summary>
    /// Add a new holding to a portfolio
    /// </summary>
    /// <param name="portfolioId">The portfolio ID to add the holding to</param>
    /// <param name="request">The holding details to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the add operation</returns>
    /// <remarks>
    /// This endpoint adds a new holding to the specified portfolio for the latest valuation date.
    /// 
    /// **Key Features:**
    /// - Creates instrument if it doesn't exist
    /// - Validates portfolio ownership
    /// - Prevents duplicate holdings for same instrument/date and platform
    /// - Calculates current value using real-time pricing
    /// - Full transaction support with rollback on errors
    /// 
    /// **Example Request:**
    /// ```json
    /// {
    ///   "platformId": 1,
    ///   "ticker": "AAPL",
    ///   "units": 100.5,
    ///   "boughtValue": 15000.00,
    ///   "instrumentName": "Apple Inc.",
    ///   "description": "Technology stock",
    ///   "currencyCode": "USD"
    /// }
    /// ```
    /// 
    /// **Validation Rules:**
    /// - Portfolio must belong to authenticated user
    /// - Units must be greater than 0
    /// - Bought value must be non-negative
    /// - Ticker is required and max 20 characters
    /// - No duplicate holding for same instrument on latest date
    /// </remarks>
    /// <response code="201">Holding successfully added</response>
    /// <response code="400">Invalid request data or validation errors</response>
    /// <response code="404">Portfolio not found or not accessible</response>
    /// <response code="409">Duplicate holding for this instrument already exists</response>
    /// <response code="500">Internal server error occurred during add operation</response>
    [HttpPost("portfolio/{portfolioId:int}")]
    [ProducesResponseType(typeof(AddHoldingApiResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<AddHoldingApiResponse>> AddHolding(
        [FromRoute] int portfolioId,
        [FromBody] AddHoldingApiRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("AddHolding");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
       // Get account ID from authenticated user
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        logger.LogInformation("AddHolding: Retrieved AccountId from authentication: {AccountId}", accountId);
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("portfolio.id", portfolioId.ToString());
        activity?.SetTag("instrument.ticker", request.Ticker);
        activity?.SetTag("holding.units", request.Units.ToString());
        
        try
        {
            using (logger.BeginScope("Adding holding for account {AccountId}, portfolio {PortfolioId}, ticker {Ticker}", 
                accountId, portfolioId, request.Ticker))
            {
                logger.LogInformation("Processing add holding request: AccountId={AccountId}, PortfolioId={PortfolioId}, Ticker={Ticker}, Units={Units}",
                    accountId, portfolioId, request.Ticker, request.Units);
            }

            // Map API request to application DTO
            var addRequest = new AddHoldingRequest
            {
                PlatformId = request.PlatformId,
                Ticker = request.Ticker,
                Units = request.Units,
                BoughtValue = request.BoughtValue,
                InstrumentName = request.InstrumentName,
                Description = request.Description,
                InstrumentTypeId = request.InstrumentTypeId,
                CurrencyCode = request.CurrencyCode,
                QuoteUnit = request.QuoteUnit
            };

            // Call the holding service
            var result = await holdingService.AddHoldingAsync(portfolioId, addRequest, accountId, cancellationToken);

            // Map application result to API response
            var response = new AddHoldingApiResponse
            {
                Success = result.Success,
                Message = result.Message,
                Errors = result.Errors,
                HoldingId = result.CreatedHolding?.Id,
                InstrumentCreated = result.InstrumentCreated,
                CurrentPrice = result.CurrentPrice,
                CurrentValue = result.CurrentValue
            };

            // Map instrument info if available
            if (result.Instrument != null)
            {
                response.Instrument = new InstrumentInfo
                {
                    Id = result.Instrument.Id,
                    Ticker = result.Instrument.Ticker,
                    Name = result.Instrument.Name,
                    Description = result.Instrument.Description,
                    CurrencyCode = result.Instrument.CurrencyCode,
                    QuoteUnit = result.Instrument.QuoteUnit,
                    InstrumentTypeId = result.Instrument.InstrumentTypeId
                };
            }

            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Message);
                activity?.SetTag("error.type", "validation");
                
                // Check for specific error types
                if (result.Message.Contains("not found") || result.Message.Contains("not accessible"))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Portfolio Not Found",
                        Detail = result.Message,
                        Status = (int)HttpStatusCode.NotFound
                    });
                }
                
                if (result.Message.Contains("already exists") || result.Message.Contains("duplicate"))
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Duplicate Holding",
                        Detail = result.Message,
                        Status = (int)HttpStatusCode.Conflict
                    });
                }
                
                return BadRequest(new ProblemDetails
                {
                    Title = "Add Holding Failed",
                    Detail = result.Message,
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            activity?.SetTag("holding.id", response.HoldingId?.ToString() ?? "null");
            activity?.SetTag("instrument.created", response.InstrumentCreated.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            stopwatch.Stop();
            logger.LogInformation("Successfully added holding {HoldingId} for ticker {Ticker} to portfolio {PortfolioId}", 
                response.HoldingId, request.Ticker, portfolioId);

            return CreatedAtAction(nameof(GetHoldingsByDate), 
                new { valuationDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }, 
                response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            logger.LogError(ex, "Error adding holding for ticker {Ticker} to portfolio {PortfolioId}", 
                request.Ticker, portfolioId);
                
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while adding the holding",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    /// <summary>
    /// Update the units of an existing holding
    /// </summary>
    /// <param name="holdingId">The ID of the holding to update</param>
    /// <param name="request">The new unit amount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the update operation</returns>
    /// <remarks>
    /// This endpoint updates the unit amount for an existing holding on the latest valuation date.
    /// The current value is automatically recalculated using the latest market price.
    /// 
    /// **Key Features:**
    /// - Updates holding units with real-time value recalculation
    /// - Validates holding ownership
    /// - Only operates on latest valuation date
    /// - Returns before/after values for comparison
    /// - Full transaction support with rollback on errors
    /// 
    /// **Example Request:**
    /// ```json
    /// {
    ///   "units": 150.75
    /// }
    /// ```
    /// 
    /// **Validation Rules:**
    /// - Holding must belong to authenticated user
    /// - Units must be greater than 0
    /// - Holding must exist on latest valuation date
    /// </remarks>
    /// <response code="200">Holding successfully updated</response>
    /// <response code="400">Invalid request data or validation errors</response>
    /// <response code="404">Holding not found or not accessible</response>
    /// <response code="500">Internal server error occurred during update operation</response>
    [HttpPut("{holdingId:int}/units")]
    [ProducesResponseType(typeof(UpdateHoldingApiResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<UpdateHoldingApiResponse>> UpdateHoldingUnits(
        [FromRoute] int holdingId,
        [FromBody] UpdateHoldingUnitsApiRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("UpdateHoldingUnits");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Get account ID from authenticated user
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        logger.LogInformation("UpdateHoldingUnits: Retrieved AccountId from authentication: {AccountId}", accountId);
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("holding.id", holdingId.ToString());
        activity?.SetTag("new.units", request.Units.ToString());
        
        try
        {
            using (logger.BeginScope("Updating holding units for account {AccountId}, holding {HoldingId}", 
                accountId, holdingId))
            {
                logger.LogInformation("Processing update holding units request: AccountId={AccountId}, HoldingId={HoldingId}, NewUnits={NewUnits}",
                    accountId, holdingId, request.Units);
            }

            // Call the holding service
            var result = await holdingService.UpdateHoldingUnitsAsync(holdingId, request.Units, accountId, cancellationToken);

            // Map application result to API response
            var response = new UpdateHoldingApiResponse
            {
                Success = result.Success,
                Message = result.Message,
                Errors = result.Errors,
                HoldingId = holdingId,
                PreviousUnits = result.PreviousUnits,
                NewUnits = result.NewUnits,
                PreviousCurrentValue = result.PreviousCurrentValue,
                NewCurrentValue = result.NewCurrentValue,
                Ticker = result.UpdatedHolding?.Instrument?.Ticker
            };

            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Message);
                activity?.SetTag("error.type", "validation");
                
                if (result.Message.Contains("not found") || result.Message.Contains("not accessible"))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Holding Not Found",
                        Detail = result.Message,
                        Status = (int)HttpStatusCode.NotFound
                    });
                }
                
                return BadRequest(new ProblemDetails
                {
                    Title = "Update Holding Failed",
                    Detail = result.Message,
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            activity?.SetTag("previous.units", response.PreviousUnits.ToString());
            activity?.SetTag("ticker", response.Ticker ?? "unknown");
            activity?.SetStatus(ActivityStatusCode.Ok);

            stopwatch.Stop();
            logger.LogInformation("Successfully updated holding {HoldingId} units from {PreviousUnits} to {NewUnits}", 
                holdingId, response.PreviousUnits, response.NewUnits);

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            logger.LogError(ex, "Error updating holding {HoldingId} units to {NewUnits}", holdingId, request.Units);
                
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while updating the holding",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    /// <summary>
    /// Delete a holding from a portfolio
    /// </summary>
    /// <param name="holdingId">The ID of the holding to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the delete operation</returns>
    /// <remarks>
    /// This endpoint removes a holding from the latest valuation date.
    /// The holding is permanently deleted and cannot be recovered.
    /// 
    /// **Key Features:**
    /// - Deletes holding from latest valuation date only
    /// - Validates holding ownership
    /// - Returns confirmation details
    /// - Full transaction support with rollback on errors
    /// 
    /// **Security:**
    /// - Only holdings belonging to authenticated user can be deleted
    /// - Cannot delete holdings from historical dates
    /// 
    /// **Validation Rules:**
    /// - Holding must belong to authenticated user
    /// - Holding must exist on latest valuation date
    /// </remarks>
    /// <response code="200">Holding successfully deleted</response>
    /// <response code="404">Holding not found or not accessible</response>
    /// <response code="500">Internal server error occurred during delete operation</response>
    [HttpDelete("{holdingId:int}")]
    [ProducesResponseType(typeof(DeleteHoldingApiResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DeleteHoldingApiResponse>> DeleteHolding(
        [FromRoute] int holdingId,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("DeleteHolding");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Get account ID from authenticated user
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        logger.LogInformation("DeleteHolding: Retrieved AccountId from authentication: {AccountId}", accountId);
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("holding.id", holdingId.ToString());
        
        try
        {
            using (logger.BeginScope("Deleting holding for account {AccountId}, holding {HoldingId}", 
                accountId, holdingId))
            {
                logger.LogInformation("Processing delete holding request: AccountId={AccountId}, HoldingId={HoldingId}",
                    accountId, holdingId);
            }

            // Call the holding service
            var result = await holdingService.DeleteHoldingAsync(holdingId, accountId, cancellationToken);

            // Map application result to API response
            var response = new DeleteHoldingApiResponse
            {
                Success = result.Success,
                Message = result.Message,
                Errors = result.Errors,
                DeletedHoldingId = result.DeletedHoldingId,
                DeletedTicker = result.DeletedTicker,
                PortfolioId = result.PortfolioId
            };

            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Message);
                activity?.SetTag("error.type", "validation");
                
                if (result.Message.Contains("not found") || result.Message.Contains("not accessible"))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Holding Not Found",
                        Detail = result.Message,
                        Status = (int)HttpStatusCode.NotFound
                    });
                }
                
                return BadRequest(new ProblemDetails
                {
                    Title = "Delete Holding Failed",
                    Detail = result.Message,
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            activity?.SetTag("deleted.ticker", response.DeletedTicker ?? "unknown");
            activity?.SetTag("portfolio.id", response.PortfolioId.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            stopwatch.Stop();
            logger.LogInformation("Successfully deleted holding {HoldingId} for ticker {Ticker} from portfolio {PortfolioId}", 
                holdingId, response.DeletedTicker, response.PortfolioId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            logger.LogError(ex, "Error deleting holding {HoldingId}", holdingId);
                
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while deleting the holding",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }
}
