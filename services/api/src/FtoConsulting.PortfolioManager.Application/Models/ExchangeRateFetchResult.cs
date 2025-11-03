namespace FtoConsulting.PortfolioManager.Application.Models;

/// <summary>
/// Result of an exchange rate fetching operation
/// </summary>
public class ExchangeRateFetchResult
{
    /// <summary>
    /// Total number of currency pairs processed
    /// </summary>
    public int TotalCurrencyPairs { get; set; }

    /// <summary>
    /// Number of exchange rates successfully fetched and persisted
    /// </summary>
    public int SuccessfulRates { get; set; }

    /// <summary>
    /// Number of exchange rates that failed to fetch
    /// </summary>
    public int FailedRates { get; set; }

    /// <summary>
    /// Number of exchange rates rolled forward from previous dates
    /// </summary>
    public int RolledForwardRates { get; set; }

    /// <summary>
    /// Collection of currency pairs that failed to fetch
    /// </summary>
    public List<FailedExchangeRateData> FailedCurrencyPairs { get; set; } = new();

    /// <summary>
    /// Duration of the fetch operation
    /// </summary>
    public TimeSpan FetchDuration { get; set; }

    /// <summary>
    /// Timestamp when the fetch operation was completed
    /// </summary>
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Information about currency pairs for which exchange rate fetching failed
/// </summary>
public class FailedExchangeRateData
{
    /// <summary>
    /// Base currency (e.g., "USD")
    /// </summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Target currency (e.g., "GBP")
    /// </summary>
    public string TargetCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Currency pair (e.g., "USD/GBP")
    /// </summary>
    public string CurrencyPair => $"{BaseCurrency}/{TargetCurrency}";

    /// <summary>
    /// Error message describing why the rate fetch failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;
}