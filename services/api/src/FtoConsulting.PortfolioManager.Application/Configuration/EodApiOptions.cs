namespace FtoConsulting.PortfolioManager.Application.Configuration;

/// <summary>
/// Configuration options for EOD Historical Data API
/// </summary>
public class EodApiOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "EodApi";
    
    /// <summary>
    /// API token for EOD Historical Data service
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Base URL for EOD API (defaults to standard endpoint)
    /// </summary>
    public string BaseUrl { get; set; } = "https://eodhd.com/api";
    
    /// <summary>
    /// Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}