namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for managing AI agent prompts from configuration
/// </summary>
public interface IAgentPromptService
{
    /// <summary>
    /// Get the portfolio advisor prompt for a specific account
    /// </summary>
    /// <param name="accountId">Account ID to include in the prompt</param>
    /// <returns>Complete prompt text</returns>
    string GetPortfolioAdvisorPrompt(int accountId);
    
    /// <summary>
    /// Get a custom prompt by name with parameter substitution
    /// </summary>
    /// <param name="promptName">Name of the prompt configuration</param>
    /// <param name="parameters">Parameters for substitution</param>
    /// <returns>Complete prompt text</returns>
    string GetPrompt(string promptName, Dictionary<string, object>? parameters = null);
}
