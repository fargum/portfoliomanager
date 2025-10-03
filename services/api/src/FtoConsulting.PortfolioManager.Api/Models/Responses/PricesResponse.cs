using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Responses;

/// <summary>
/// Response containing market prices for ISINs fetched from EOD Historical Data
/// </summary>
public class PricesResponse
{
    /// <summary>
    /// The valuation date for which prices were fetched
    /// </summary>
    [Required]
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// Total number of distinct ISINs found in holdings
    /// </summary>
    [Required]
    public int TotalIsins { get; set; }

    /// <summary>
    /// Number of ISINs for which prices were successfully fetched
    /// </summary>
    [Required]
    public int SuccessfulPrices { get; set; }

    /// <summary>
    /// Number of ISINs for which price fetching failed
    /// </summary>
    [Required]
    public int FailedPrices { get; set; }

    /// <summary>
    /// Collection of successfully fetched market prices
    /// </summary>
    [Required]
    public ICollection<InstrumentPrice> Prices { get; set; } = new List<InstrumentPrice>();

    /// <summary>
    /// Collection of ISINs for which price fetching failed
    /// </summary>
    public ICollection<FailedPrice> FailedIsins { get; set; } = new List<FailedPrice>();

    /// <summary>
    /// Timestamp when the prices were fetched
    /// </summary>
    [Required]
    public DateTime FetchedAt { get; set; }

    /// <summary>
    /// Total time taken to fetch all prices (in milliseconds)
    /// </summary>
    public long FetchDurationMs { get; set; }
}

/// <summary>
/// Market price information for a specific instrument
/// </summary>
public class InstrumentPrice
{
    /// <summary>
    /// ISIN of the instrument
    /// </summary>
    [Required]
    public string ISIN { get; set; } = string.Empty;

    /// <summary>
    /// Trading symbol/ticker
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Instrument name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Current market price
    /// </summary>
    [Required]
    public decimal Price { get; set; }

    /// <summary>
    /// Currency of the price
    /// </summary>
    [Required]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Price change from previous trading day
    /// </summary>
    public decimal? Change { get; set; }

    /// <summary>
    /// Percentage change from previous trading day
    /// </summary>
    public decimal? ChangePercent { get; set; }

    /// <summary>
    /// Previous day's closing price
    /// </summary>
    public decimal? PreviousClose { get; set; }

    /// <summary>
    /// Today's opening price
    /// </summary>
    public decimal? Open { get; set; }

    /// <summary>
    /// Day's high price
    /// </summary>
    public decimal? High { get; set; }

    /// <summary>
    /// Day's low price
    /// </summary>
    public decimal? Low { get; set; }

    /// <summary>
    /// Trading volume
    /// </summary>
    public long? Volume { get; set; }

    /// <summary>
    /// Market where the instrument is traded
    /// </summary>
    public string? Market { get; set; }

    /// <summary>
    /// Market status (e.g., "Open", "Closed", "Pre-Market")
    /// </summary>
    public string? MarketStatus { get; set; }

    /// <summary>
    /// Timestamp of the price data
    /// </summary>
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Information about ISINs for which price fetching failed
/// </summary>
public class FailedPrice
{
    /// <summary>
    /// ISIN for which price fetching failed
    /// </summary>
    [Required]
    public string ISIN { get; set; } = string.Empty;

    /// <summary>
    /// Error message describing why the price fetch failed
    /// </summary>
    [Required]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Error code from the pricing service (if available)
    /// </summary>
    public string? ErrorCode { get; set; }
}