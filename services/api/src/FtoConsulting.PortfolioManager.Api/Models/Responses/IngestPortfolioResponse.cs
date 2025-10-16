namespace FtoConsulting.PortfolioManager.Api.Models.Responses;

/// <summary>
/// Response model for portfolio ingestion
/// </summary>
public class IngestPortfolioResponse
{
    /// <summary>
    /// ID of the ingested portfolio
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// Name of the portfolio
    /// </summary>
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>
    /// Account ID that owns the portfolio
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Number of holdings successfully ingested
    /// </summary>
    public int HoldingsCount { get; set; }

    /// <summary>
    /// Number of new instruments created during ingestion
    /// </summary>
    public int NewInstrumentsCreated { get; set; }

    /// <summary>
    /// Number of existing instruments updated during ingestion
    /// </summary>
    public int InstrumentsUpdated { get; set; }

    /// <summary>
    /// Total value of all holdings in the portfolio
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Total profit/loss across all holdings
    /// </summary>
    public decimal TotalProfitLoss { get; set; }

    /// <summary>
    /// Timestamp when the portfolio was ingested
    /// </summary>
    public DateTime IngestedAt { get; set; }

    /// <summary>
    /// Summary of holdings ingested
    /// </summary>
    public List<HoldingSummaryDto> Holdings { get; set; } = new();
}

/// <summary>
/// Summary information about an ingested holding
/// </summary>
public class HoldingSummaryDto
{
    /// <summary>
    /// ID of the holding
    /// </summary>
    public int HoldingId { get; set; }

    /// <summary>
    /// ID of the instrument
    /// </summary>
    public int InstrumentId { get; set; }

    /// <summary>
    /// Name of the instrument
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Number of units held
    /// </summary>
    public decimal UnitAmount { get; set; }

    /// <summary>
    /// Current market value
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Profit/loss for this holding
    /// </summary>
    public decimal ProfitLoss { get; set; }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed error information
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Validation errors (if any)
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; set; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}