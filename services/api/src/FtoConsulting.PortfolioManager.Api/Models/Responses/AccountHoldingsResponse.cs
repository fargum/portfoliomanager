using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Responses;

/// <summary>
/// Response containing a collection of flattened holdings for an account
/// </summary>
public class AccountHoldingsResponse
{
    /// <summary>
    /// Account identifier
    /// </summary>
    /// <example>12345</example>
    public int AccountId { get; set; }

    /// <summary>
    /// Valuation date for the holdings
    /// </summary>
    /// <example>2025-09-27</example>
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// Total number of holdings returned
    /// </summary>
    /// <example>25</example>
    public int TotalHoldings { get; set; }

    /// <summary>
    /// Total current value across all holdings
    /// </summary>
    /// <example>125750.50</example>
    public decimal TotalCurrentValue { get; set; }

    /// <summary>
    /// Total bought value across all holdings
    /// </summary>
    /// <example>118250.25</example>
    public decimal TotalBoughtValue { get; set; }

    /// <summary>
    /// Total gain/loss across all holdings
    /// </summary>
    /// <example>7500.25</example>
    public decimal TotalGainLoss { get; set; }

    /// <summary>
    /// Overall gain/loss percentage
    /// </summary>
    /// <example>6.34</example>
    public decimal TotalGainLossPercentage { get; set; }

    /// <summary>
    /// Indicates if the data includes real-time prices
    /// </summary>
    /// <example>true</example>
    public bool IsRealTime { get; set; }

    /// <summary>
    /// Percentage of holdings with real-time price coverage (only applicable when IsRealTime is true)
    /// </summary>
    /// <example>0.85</example>
    public decimal? PriceCoverage { get; set; }

    /// <summary>
    /// Collection of flattened holding data
    /// </summary>
    public IEnumerable<FlattenedHoldingResponse> Holdings { get; set; } = new List<FlattenedHoldingResponse>();
}