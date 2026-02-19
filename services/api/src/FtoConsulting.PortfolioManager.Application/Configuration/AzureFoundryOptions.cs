namespace FtoConsulting.PortfolioManager.Application.Configuration;

/// <summary>
/// Configuration options for Azure AI Foundry
/// </summary>
public class AzureFoundryOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AzureFoundry";

    /// <summary>
    /// Legacy Azure OpenAI endpoint (kept for backward compat, no longer used by the AI agent)
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure AI Foundry project endpoint
    /// e.g. https://neiltest.services.ai.azure.com/api/projects/proj-llms
    /// All deployed models are reached through this single endpoint.
    /// </summary>
    public string FoundryProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Shared API key — works for both the legacy Azure OpenAI resource and the Foundry project.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default model deployment name used when the caller does not specify one.
    /// </summary>
    public string ModelName { get; set; } = "gpt-5-mini";

    /// <summary>
    /// Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Models available for selection in the UI.
    /// Add entries here to expose additional Foundry deployments without code changes.
    /// </summary>
    public List<ModelConfig> AvailableModels { get; set; } = [];
}

/// <summary>
/// A single model deployment exposed to the UI selector.
/// </summary>
public class ModelConfig
{
    /// <summary>Deployment name in Azure AI Foundry (passed as the model field in API requests).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the UI dropdown.</summary>
    public string DisplayName { get; set; } = string.Empty;
}