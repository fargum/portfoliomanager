namespace FtoConsulting.PortfolioManager.Application.Models;

/// <summary>
/// Result of a holding revaluation operation
/// </summary>
public class HoldingRevaluationResult
{
    /// <summary>
    /// The valuation date for which holdings were revalued
    /// </summary>
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// The source valuation date from which holdings were copied
    /// </summary>
    public DateOnly? SourceValuationDate { get; set; }

    /// <summary>
    /// Total number of holdings processed
    /// </summary>
    public int TotalHoldings { get; set; }

    /// <summary>
    /// Number of holdings successfully revalued
    /// </summary>
    public int SuccessfulRevaluations { get; set; }

    /// <summary>
    /// Number of holdings that failed revaluation (no price data available)
    /// </summary>
    public int FailedRevaluations { get; set; }

    /// <summary>
    /// Number of existing holdings that were replaced
    /// </summary>
    public int ReplacedHoldings { get; set; }

    /// <summary>
    /// Duration of the revaluation operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// When the revaluation was performed
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// List of instruments that failed revaluation
    /// </summary>
    public List<FailedRevaluationData> FailedInstruments { get; set; } = new List<FailedRevaluationData>();
}

/// <summary>
/// Information about an instrument that failed revaluation
/// </summary>
public class FailedRevaluationData
{
    /// <summary>
    /// Ticker of the instrument
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Name of the instrument
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Reason for failure
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Error code
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;
}