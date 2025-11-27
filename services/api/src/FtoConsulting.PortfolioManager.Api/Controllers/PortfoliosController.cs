using FtoConsulting.PortfolioManager.Api.Models.Requests;
using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Api.Controllers;

/// <summary>
/// Portfolio management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "RequirePortfolioScope")]
public class PortfoliosController(
    IPortfolioIngest portfolioIngest,
    IPortfolioMappingService mappingService,
    ILogger<PortfoliosController> logger,
    MetricsService metrics) : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Portfolios");

    /// <summary>
    /// Ingest portfolio holdings data
    /// </summary>
    /// <param name="request">Portfolio and holdings data to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of the ingested portfolio</returns>
    /// <remarks>
    /// This endpoint accepts a portfolio with a collection of holdings and their associated instruments.
    /// 
    /// **Key Features:**
    /// - **Automatic Instrument Management**: Instruments are identified by Ticker. If an instrument with the same Ticker already exists, it will be reused. If not, a new instrument will be created.
    /// - **Portfolio Creation/Update**: If a portfolio with the same ID exists, it will be updated. Otherwise, a new portfolio is created.
    /// - **Transaction Safety**: All operations are wrapped in a database transaction with automatic rollback on errors.
    /// - **Validation**: Input data is validated according to business rules and data constraints.
    /// 
    /// **Sample Request:**
    /// ```json
    /// {
    ///   "portfolioName": "My Investment Portfolio",
    ///   "accountId": "12345678,
    ///   "holdings": [
    ///     {
    ///       "valuationDate": "2024-01-15T00:00:00Z",
    ///       "platformId": "8765432",
    ///       "unitAmount": 100.0,
    ///       "boughtValue": 15000.00,
    ///       "currentValue": 18500.00,
    ///       "dailyProfitLoss": 250.00,
    ///       "dailyProfitLossPercentage": 1.37,
    ///       "instrument": {
    ///         "isin": "APL",
    ///         "name": "Apple Inc",
    ///         "description": "Apple Inc Common Stock",
    ///         "instrumentTypeId": "1"
    ///       }
    ///     }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Portfolio successfully ingested</response>
    /// <response code="400">Invalid request data or validation errors</response>
    /// <response code="500">Internal server error during ingestion</response>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(IngestPortfolioResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IngestPortfolioResponse>> IngestPortfolio(
        [FromBody] IngestPortfolioRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("IngestPortfolio");
        var stopwatch = Stopwatch.StartNew();
        var accountId = request?.AccountId.ToString();
        
        activity?.SetTag("portfolio.name", request?.PortfolioName ?? "unknown");
        activity?.SetTag("account.id", accountId ?? "unknown");
        activity?.SetTag("holdings.count", request?.Holdings?.Count.ToString() ?? "0");
        
        try
        {
            using (logger.BeginScope("Portfolio ingestion for {PortfolioName} with {HoldingsCount} holdings", request?.PortfolioName ?? "Unknown", request?.Holdings?.Count ?? 0))
            {
                logger.LogInformation("Starting portfolio ingestion for PortfolioName={PortfolioName}, AccountId={AccountId}, HoldingsCount={HoldingsCount}",
                    request?.PortfolioName ?? "Unknown", request?.AccountId, request?.Holdings?.Count ?? 0);
            }

            if (request == null)
            {
                activity?.SetTag("validation.result", "failed");
                activity?.SetTag("validation.reason", "null_request");
                return BadRequest(new ErrorResponse
                {
                    Message = "Request cannot be null"
                });
            }

            // Validate the request
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new ErrorResponse
                {
                    Message = "Validation failed",
                    ValidationErrors = validationErrors
                });
            }

            // Map DTO to domain entity
            var portfolio = mappingService.MapToPortfolio(request);

            // Ingest the portfolio using our domain service
            var ingestedPortfolio = await portfolioIngest.IngestPortfolioAsync(portfolio, cancellationToken);

            // Map result back to response DTO
            var response = mappingService.MapToResponse(ingestedPortfolio);

            logger.LogInformation("Successfully ingested portfolio '{PortfolioName}' with ID {PortfolioId}", 
                ingestedPortfolio.Name, ingestedPortfolio.Id);

            // Record successful metrics
            stopwatch.Stop();
            metrics.IncrementPortfolioIngestions(accountId, "success");
            metrics.RecordPortfolioIngestDuration(stopwatch.Elapsed.TotalSeconds, accountId, "success");

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "validation");
            
            stopwatch.Stop();
            metrics.IncrementPortfolioIngestions(accountId, "validation_error");
            metrics.RecordPortfolioIngestDuration(stopwatch.Elapsed.TotalSeconds, accountId, "validation_error");
            
            logger.LogWarning(ex, "Invalid argument provided for portfolio ingestion");
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid request data",
                Details = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "invalid_operation");
            
            stopwatch.Stop();
            metrics.IncrementPortfolioIngestions(accountId, "operation_error");
            metrics.RecordPortfolioIngestDuration(stopwatch.Elapsed.TotalSeconds, accountId, "operation_error");
            
            logger.LogWarning(ex, "Invalid operation during portfolio ingestion");
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid operation",
                Details = ex.Message
            });
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "unexpected");
            
            stopwatch.Stop();
            metrics.IncrementPortfolioIngestions(accountId, "system_error");
            metrics.RecordPortfolioIngestDuration(stopwatch.Elapsed.TotalSeconds, accountId, "system_error");
            
            logger.LogError(ex, "Unexpected error during portfolio ingestion for portfolio '{PortfolioName}'", 
                request?.PortfolioName ?? "Unknown");
            
            return StatusCode(500, new ErrorResponse
            {
                Message = "An unexpected error occurred during portfolio ingestion",
                Details = "Please check the logs for more details"
            });
        }
    }
}
