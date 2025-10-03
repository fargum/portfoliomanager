namespace FtoConsulting.PortfolioManager.Application.Models;

/// <summary>
/// Result of a price fetching operation containing both successful and failed price retrievals
/// </summary>
public class PriceFetchResult
{
    /// <summary>
    /// Total number of distinct ISINs that were processed
    /// </summary>
    public int TotalIsins { get; set; }

    /// <summary>
    /// Number of ISINs for which prices were successfully fetched
    /// </summary>
    public int SuccessfulPrices => Prices.Count;

    /// <summary>
    /// Number of ISINs for which price fetching failed
    /// </summary>
    public int FailedPrices => FailedIsins.Count;

    /// <summary>
    /// Collection of successfully fetched instrument prices
    /// </summary>
    public List<InstrumentPriceData> Prices { get; set; } = new();

    /// <summary>
    /// Collection of ISINs for which price fetching failed
    /// </summary>
    public List<FailedPriceData> FailedIsins { get; set; } = new();

    /// <summary>
    /// Time taken to complete the price fetching operation
    /// </summary>
    public TimeSpan FetchDuration { get; set; }

    /// <summary>
    /// Timestamp when the fetch operation was completed
    /// </summary>
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Market price data for a specific instrument
/// </summary>
public class InstrumentPriceData
{
    public string ISIN { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }
    public decimal? PreviousClose { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public long? Volume { get; set; }
    public string? Market { get; set; }
    public string? MarketStatus { get; set; }
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Information about a failed price fetch attempt
/// </summary>
public class FailedPriceData
{
    public string ISIN { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}