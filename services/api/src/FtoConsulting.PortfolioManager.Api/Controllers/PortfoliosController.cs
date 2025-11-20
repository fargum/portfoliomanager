using FtoConsulting.PortfolioManager.Api.Models.Requests;
using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application.Services;
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
public class PortfoliosController : ControllerBase
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.Portfolios");
    
    private readonly IPortfolioIngest _portfolioIngest;
    private readonly IPortfolioMappingService _mappingService;
    private readonly ILogger<PortfoliosController> _logger;

    /// <summary>
    /// Initializes a new instance of the PortfoliosController
    /// </summary>
    public PortfoliosController(
        IPortfolioIngest portfolioIngest,
        IPortfolioMappingService mappingService,
        ILogger<PortfoliosController> logger)
    {
        _portfolioIngest = portfolioIngest;
        _mappingService = mappingService;
        _logger = logger;
    }

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
        activity?.SetTag("portfolio.name", request?.PortfolioName ?? "unknown");
        activity?.SetTag("account.id", request?.AccountId.ToString() ?? "unknown");
        activity?.SetTag("holdings.count", request?.Holdings?.Count.ToString() ?? "0");
        
        try
        {
            _logger.LogInformation("Starting portfolio ingestion for portfolio '{PortfolioName}' with {HoldingCount} holdings", 
                request?.PortfolioName ?? "Unknown", request?.Holdings?.Count ?? 0);

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
            var portfolio = _mappingService.MapToPortfolio(request);

            // Ingest the portfolio using our domain service
            var ingestedPortfolio = await _portfolioIngest.IngestPortfolioAsync(portfolio, cancellationToken);

            // Map result back to response DTO
            var response = _mappingService.MapToResponse(ingestedPortfolio);

            _logger.LogInformation("Successfully ingested portfolio '{PortfolioName}' with ID {PortfolioId}", 
                ingestedPortfolio.Name, ingestedPortfolio.Id);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "validation");
            _logger.LogWarning(ex, "Invalid argument provided for portfolio ingestion");
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
            _logger.LogWarning(ex, "Invalid operation during portfolio ingestion");
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
            _logger.LogError(ex, "Unexpected error during portfolio ingestion for portfolio '{PortfolioName}'", 
                request?.PortfolioName ?? "Unknown");
            
            return StatusCode(500, new ErrorResponse
            {
                Message = "An unexpected error occurred during portfolio ingestion",
                Details = "Please check the logs for more details"
            });
        }
    }

    /// <summary>
    /// Ingest multiple portfolios in a single batch operation
    /// </summary>
    /// <param name="requests">Collection of portfolios to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of all ingested portfolios</returns>
    /// <remarks>
    /// This endpoint allows ingesting multiple portfolios in a single transaction for better performance.
    /// All portfolios will be processed together, and if any fails, the entire batch will be rolled back.
    /// 
    /// **Benefits of Batch Processing:**
    /// - **Performance**: Reduced database round trips and optimized instrument deduplication
    /// - **Consistency**: All portfolios succeed or fail together
    /// - **Efficiency**: Instruments shared across portfolios are processed only once
    /// </remarks>
    /// <response code="200">All portfolios successfully ingested</response>
    /// <response code="400">Invalid request data or validation errors</response>
    /// <response code="500">Internal server error during batch ingestion</response>
    [HttpPost("ingest-batch")]
    [ProducesResponseType(typeof(List<IngestPortfolioResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<List<IngestPortfolioResponse>>> IngestPortfoliosBatch(
        [FromBody] List<IngestPortfolioRequest> requests,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("IngestPortfoliosBatch");
        activity?.SetTag("portfolios.count", requests?.Count.ToString() ?? "0");
        activity?.SetTag("operation.type", "batch_ingest");
        
        try
        {
            _logger.LogInformation("Starting batch portfolio ingestion for {PortfolioCount} portfolios", requests?.Count ?? 0);

            if (requests == null || requests.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "No portfolios provided for ingestion"
                });
            }

            // Validate all requests
            var validationErrors = new Dictionary<string, string[]>();
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (string.IsNullOrEmpty(request.PortfolioName))
                {
                    validationErrors[$"requests[{i}].portfolioName"] = new[] { "Portfolio name is required" };
                }
                if (request.AccountId <= 0)
                {
                    validationErrors[$"requests[{i}].accountId"] = new[] { "Account ID must be a positive integer" };
                }
                if (request.Holdings == null || request.Holdings.Count == 0)
                {
                    validationErrors[$"requests[{i}].holdings"] = new[] { "At least one holding is required" };
                }
            }

            if (validationErrors.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Validation failed for batch request",
                    ValidationErrors = validationErrors
                });
            }

            // Map DTOs to domain entities
            var portfolios = requests.Select(r => _mappingService.MapToPortfolio(r)).ToList();

            // Ingest all portfolios using batch operation
            var ingestedPortfolios = await _portfolioIngest.IngestPortfoliosAsync(portfolios, cancellationToken);

            // Map results back to response DTOs
            var responses = ingestedPortfolios.Select(p => _mappingService.MapToResponse(p)).ToList();

            _logger.LogInformation("Successfully ingested {PortfolioCount} portfolios in batch", responses.Count);

            return Ok(responses);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument provided for batch portfolio ingestion");
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid request data",
                Details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during batch portfolio ingestion");
            
            return StatusCode(500, new ErrorResponse
            {
                Message = "An unexpected error occurred during batch portfolio ingestion",
                Details = "Please check the logs for more details"
            });
        }
    }
}
