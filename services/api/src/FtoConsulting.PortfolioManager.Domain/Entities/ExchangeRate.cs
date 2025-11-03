namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Exchange rate entity for currency conversions
/// </summary>
public class ExchangeRate : BaseEntity
{
    /// <summary>
    /// Base currency (e.g., "USD")
    /// </summary>
    public string BaseCurrency { get; private set; } = string.Empty;
    
    /// <summary>
    /// Target currency (e.g., "GBP")
    /// </summary>
    public string TargetCurrency { get; private set; } = string.Empty;
    
    /// <summary>
    /// Exchange rate value (1 BaseCurrency = Rate TargetCurrency)
    /// </summary>
    public decimal Rate { get; private set; }
    
    /// <summary>
    /// Date the rate is valid for
    /// </summary>
    public DateOnly RateDate { get; private set; }
    
    /// <summary>
    /// Source of the exchange rate (e.g., "EOD", "MANUAL", "CENTRAL_BANK")
    /// </summary>
    public string Source { get; private set; } = string.Empty;
    
    /// <summary>
    /// When this rate was fetched/updated
    /// </summary>
    public DateTime FetchedAt { get; private set; }

    // Private constructor for EF Core
    private ExchangeRate() { }

    public ExchangeRate(string baseCurrency, string targetCurrency, decimal rate, DateOnly rateDate, string source)
    {
        BaseCurrency = baseCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(baseCurrency));
        TargetCurrency = targetCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(targetCurrency));
        Rate = rate > 0 ? rate : throw new ArgumentException("Rate must be positive", nameof(rate));
        RateDate = rateDate;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        FetchedAt = DateTime.UtcNow;
    }

    public void UpdateRate(decimal newRate, string source)
    {
        Rate = newRate > 0 ? newRate : throw new ArgumentException("Rate must be positive", nameof(newRate));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        FetchedAt = DateTime.UtcNow;
        SetUpdatedAt();
    }
}