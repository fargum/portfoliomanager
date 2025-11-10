namespace FtoConsulting.PortfolioManager.Domain.Constants;

/// <summary>
/// Constants for currency codes and quote units
/// </summary>
public static class CurrencyConstants
{
    /// <summary>
    /// British Pound Sterling
    /// </summary>
    public const string GBP = "GBP";
    
    /// <summary>
    /// British Pence (1/100th of a pound)
    /// </summary>
    public const string GBX = "GBX";
    
    /// <summary>
    /// United States Dollar
    /// </summary>
    public const string USD = "USD";
    
    /// <summary>
    /// Euro
    /// </summary>
    public const string EUR = "EUR";
    
    /// <summary>
    /// Default base currency for portfolio calculations
    /// </summary>
    public const string DEFAULT_BASE_CURRENCY = GBP;
    
    /// <summary>
    /// Default quote unit when not specified
    /// </summary>
    public const string DEFAULT_QUOTE_UNIT = GBP;
}