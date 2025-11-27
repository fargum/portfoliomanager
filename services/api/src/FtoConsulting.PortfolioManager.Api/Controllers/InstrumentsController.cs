using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Api.Models.Responses;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Controller for managing instruments
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class InstrumentsController(
    IInstrumentRepository instrumentRepository,
    ICurrentUserService currentUserService,
    ILogger<InstrumentsController> logger) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Api.Instruments");

    /// <summary>
    /// Check if an instrument exists by ticker
    /// </summary>
    /// <param name="ticker">The ticker symbol to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Information about whether the instrument exists and its details if found</returns>
    /// <response code="200">Instrument check completed successfully</response>
    /// <response code="400">Invalid ticker provided</response>
    /// <response code="401">Unauthorized access</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("check/{ticker}")]
    [ProducesResponseType(typeof(InstrumentCheckApiResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<InstrumentCheckApiResponse>> CheckInstrument(
        [FromRoute] string ticker,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("CheckInstrument");
        
        // Get account ID from authenticated user
        var accountId = await currentUserService.GetCurrentUserAccountIdAsync();
        
        activity?.SetTag("account.id", accountId.ToString());
        activity?.SetTag("instrument.ticker", ticker);
        
        try
        {
            // Validate ticker
            if (string.IsNullOrWhiteSpace(ticker))
            {
                logger.LogWarning("Invalid ticker provided for instrument check: {Ticker}", ticker);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Ticker",
                    Detail = "Ticker cannot be null, empty, or whitespace",
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            // Clean and normalize ticker
            var normalizedTicker = ticker.Trim().ToUpperInvariant();
            
            logger.LogInformation("Checking if instrument exists for ticker {Ticker} (account {AccountId})", 
                normalizedTicker, accountId);

            // Check if instrument exists using repository
            var existingInstrument = await instrumentRepository.GetByTickerAsync(normalizedTicker);
            
            var response = new InstrumentCheckApiResponse
            {
                Ticker = normalizedTicker,
                Exists = existingInstrument != null
            };

            // Include instrument details if it exists
            if (existingInstrument != null)
            {
                response.Instrument = new InstrumentInfo
                {
                    Id = existingInstrument.Id,
                    Ticker = existingInstrument.Ticker,
                    Name = existingInstrument.Name,
                    Description = existingInstrument.Description,
                    CurrencyCode = existingInstrument.CurrencyCode,
                    QuoteUnit = existingInstrument.QuoteUnit,
                    InstrumentTypeId = existingInstrument.InstrumentTypeId
                };
                
                logger.LogDebug("Instrument {Ticker} found with ID {InstrumentId}", 
                    normalizedTicker, existingInstrument.Id);
            }
            else
            {
                logger.LogDebug("Instrument {Ticker} not found in database", normalizedTicker);
            }

            activity?.SetTag("instrument.exists", response.Exists.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking instrument existence for ticker {Ticker}", ticker);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return Problem(
                title: "Instrument Check Failed",
                detail: "An error occurred while checking instrument existence",
                statusCode: (int)HttpStatusCode.InternalServerError
            );
        }
    }
}
