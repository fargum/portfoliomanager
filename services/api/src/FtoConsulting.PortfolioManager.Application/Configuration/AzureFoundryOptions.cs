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
    /// Azure AI Foundry inference endpoint — the /openai/v1/ path works for all deployed models
    /// (OpenAI GPT, Grok, DeepSeek, etc.) via the OpenAI SDK with a custom endpoint.
    /// e.g. https://neiltest.services.ai.azure.com/openai/v1/
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

    /// <summary>
    /// Whether this model supports tool/function calling.
    /// Models hosted via vLLM (e.g. Llama, Phi) may not support auto tool choice.
    /// Defaults to true.
    /// </summary>
    public bool SupportsTools { get; set; } = true;
}