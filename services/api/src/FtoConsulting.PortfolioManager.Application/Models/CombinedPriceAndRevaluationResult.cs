namespace FtoConsulting.PortfolioManager.Application.Models;

/// <summary>
/// Combined result of price fetching and holding revaluation operations
/// </summary>
public class CombinedPriceAndRevaluationResult
{
    /// <summary>
    /// The valuation date for which operations were performed
    /// </summary>
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// Result of the price fetching operation
    /// </summary>
    public PriceFetchResult PriceFetchResult { get; set; } = new();

    /// <summary>
    /// Result of the holding revaluation operation
    /// </summary>
    public HoldingRevaluationResult HoldingRevaluationResult { get; set; } = new();

    /// <summary>
    /// Overall success status of both operations
    /// </summary>
    public bool OverallSuccess => 
        PriceFetchResult.SuccessfulPrices > 0 && 
        HoldingRevaluationResult.SuccessfulRevaluations > 0;

    /// <summary>
    /// Total duration of both operations combined
    /// </summary>
    public TimeSpan TotalDuration => 
        PriceFetchResult.FetchDuration + HoldingRevaluationResult.Duration;

    /// <summary>
    /// When the combined operation was started
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Summary of the combined operation
    /// </summary>
    public string Summary => 
        $"Fetched {PriceFetchResult.SuccessfulPrices}/{PriceFetchResult.TotalTickers} prices, " +
        $"revalued {HoldingRevaluationResult.SuccessfulRevaluations}/{HoldingRevaluationResult.TotalHoldings} holdings " +
        $"in {TotalDuration.TotalMilliseconds:F0}ms";
}