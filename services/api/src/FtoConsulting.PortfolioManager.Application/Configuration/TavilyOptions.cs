namespace FtoConsulting.PortfolioManager.Application.Configuration;

/// <summary>
/// Configuration options for the Tavily search API
/// </summary>
public class TavilyOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Tavily";

    /// <summary>
    /// Tavily API key (Bearer token)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the Tavily API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.tavily.com";

    /// <summary>
    /// Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
