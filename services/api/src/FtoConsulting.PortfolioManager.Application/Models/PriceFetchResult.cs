namespace FtoConsulting.PortfolioManager.Application.Models;

/// <summary>
/// Result of a price fetching operation containing both successful and failed price retrievals
/// </summary>
public class PriceFetchResult
{
    /// <summary>
    /// Total number of distinct tickers that were processed
    /// </summary>
    public int TotalTickers { get; set; }

    /// <summary>
    /// Number of tickers for which prices were successfully fetched and persisted
    /// </summary>
    public int SuccessfulPrices { get; set; }

    /// <summary>
    /// Number of ISINs for which price fetching failed
    /// </summary>
    public int FailedPrices => FailedTickers.Count;

    /// <summary>
    /// Collection of ISINs for which price fetching failed
    /// Note: Successful price data is now persisted to the database instead of being returned here
    /// </summary>
    public List<FailedPriceData> FailedTickers { get; set; } = new();

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
    public string Ticker { get; set; } = string.Empty;
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
    public string Ticker { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}