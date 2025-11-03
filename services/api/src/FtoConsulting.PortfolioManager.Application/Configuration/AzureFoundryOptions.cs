namespace FtoConsulting.PortfolioManager.Application.Configuration;

/// <summary>
/// Configuration options for Azure Foundry Inference Endpoint
/// </summary>
public class AzureFoundryOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AzureFoundry";
    
    /// <summary>
    /// Azure Foundry inference endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// API key for Azure Foundry service
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Chat model name to use for AI requests
    /// </summary>
    public string ModelName { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}