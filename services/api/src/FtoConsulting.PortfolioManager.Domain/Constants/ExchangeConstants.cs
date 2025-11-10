namespace FtoConsulting.PortfolioManager.Domain.Constants;

/// <summary>
/// Constants for stock exchange suffixes and related mappings
/// </summary>
public static class ExchangeConstants
{
    /// <summary>
    /// London Stock Exchange suffixes
    /// </summary>
    public const string LSE_SUFFIX = ".LSE";
    public const string LONDON_SUFFIX = ".L";
    
    /// <summary>
    /// United States exchange suffixes
    /// </summary>
    public const string US_SUFFIX = ".US";
    
    /// <summary>
    /// European exchange suffixes
    /// </summary>
    public const string PARIS_SUFFIX = ".PA";
    public const string DEUTSCHE_SUFFIX = ".DE";
    
    /// <summary>
    /// Special instrument tickers
    /// </summary>
    public const string CASH_TICKER = "CASH";
    
    /// <summary>
    /// Proxy instrument requiring special pricing logic
    /// </summary>
    public const string ISF_TICKER = "ISF.LSE";
    
    /// <summary>
    /// Scaling factor for ISF.LSE proxy instrument
    /// TODO: Move this to configuration or database
    /// </summary>
    public const decimal ISF_SCALING_FACTOR = 3.362m;
}