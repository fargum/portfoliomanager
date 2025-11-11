using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

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

/// <summary>
/// Implementation of agent prompt service that loads prompts from JSON configuration
/// </summary>
public class AgentPromptService : IAgentPromptService
{
    private readonly ILogger<AgentPromptService> _logger;
    private readonly AgentPromptConfiguration _promptConfig;

    public AgentPromptService(ILogger<AgentPromptService> logger)
    {
        _logger = logger;
        _promptConfig = LoadPromptConfiguration();
    }

    public string GetPortfolioAdvisorPrompt(int accountId)
    {
        try
        {
            var config = _promptConfig.PortfolioAdvisor;
            var promptBuilder = new StringBuilder();

            // Base instructions with account ID
            promptBuilder.AppendLine(config.BaseInstructions.Replace("{accountId}", accountId.ToString()));
            promptBuilder.AppendLine();

            // Tool usage guidance
            promptBuilder.AppendLine("WHEN TO USE YOUR TOOLS:");
            promptBuilder.AppendLine("You have some great tools at your disposal, but only use them when someone actually wants portfolio or market information:");
            promptBuilder.AppendLine();
            
            promptBuilder.AppendLine("✅ Perfect times to use tools:");
            foreach (var example in config.ToolUsageGuidance.WhenToUseTools)
            {
                promptBuilder.AppendLine($"- \"{example}\"");
            }
            promptBuilder.AppendLine();
            
            promptBuilder.AppendLine("❌ Just have a normal chat for:");
            foreach (var example in config.ToolUsageGuidance.WhenNotToUseTools)
            {
                promptBuilder.AppendLine($"- {example}\"");
            }
            promptBuilder.AppendLine();

            // Communication style
            promptBuilder.AppendLine("COMMUNICATION STYLE:");
            promptBuilder.AppendLine(config.CommunicationStyle.Approach);
            promptBuilder.AppendLine();
            
            promptBuilder.AppendLine("❌ Avoid this robotic style:");
            promptBuilder.AppendLine($"## {config.CommunicationStyle.BadExample.Title}");
            
            if (config.CommunicationStyle.BadExample.Content is string[] badArray)
            {
                foreach (var item in badArray)
                {
                    promptBuilder.AppendLine($"- {item}");
                }
            }
            else if (config.CommunicationStyle.BadExample.Content is string badString)
            {
                promptBuilder.AppendLine($"- {badString}");
            }
            promptBuilder.AppendLine();
            
            promptBuilder.AppendLine("✅ Go for this friendly approach:");
            promptBuilder.AppendLine($"## {config.CommunicationStyle.GoodExample.Title}");
            promptBuilder.AppendLine();
            
            if (config.CommunicationStyle.GoodExample.Content is string goodString)
            {
                promptBuilder.AppendLine(goodString);
            }
            else if (config.CommunicationStyle.GoodExample.Content is string[] goodArray)
            {
                foreach (var item in goodArray)
                {
                    promptBuilder.AppendLine(item);
                }
            }
            promptBuilder.AppendLine();

            // Formatting guidelines
            promptBuilder.AppendLine("FORMATTING THAT FEELS NATURAL:");
            foreach (var guideline in config.FormattingGuidelines)
            {
                promptBuilder.AppendLine($"- {guideline}");
            }
            promptBuilder.AppendLine();

            // Table example
            promptBuilder.AppendLine("TABLES WHEN NEEDED:");
            promptBuilder.AppendLine(config.TableExample.Description);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(config.TableExample.Format);
            promptBuilder.AppendLine();

            // Key reminders
            promptBuilder.AppendLine("REMEMBER:");
            foreach (var reminder in config.KeyReminders)
            {
                promptBuilder.AppendLine($"- {reminder}");
            }
            promptBuilder.AppendLine();

            // Personality
            promptBuilder.AppendLine(config.Personality);

            return promptBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building portfolio advisor prompt for account {AccountId}", accountId);
            
            // Fallback to a basic prompt if configuration fails
            return $"You are a helpful financial advisor for Account ID {accountId}. Provide clear, friendly assistance with portfolio questions.";
        }
    }

    public string GetPrompt(string promptName, Dictionary<string, object>? parameters = null)
    {
        // For now, only handle PortfolioAdvisor
        // This can be extended for other prompt types in the future
        if (promptName == "PortfolioAdvisor" && parameters?.ContainsKey("accountId") == true)
        {
            var accountId = Convert.ToInt32(parameters["accountId"]);
            return GetPortfolioAdvisorPrompt(accountId);
        }

        _logger.LogWarning("Unknown prompt name: {PromptName}", promptName);
        return "You are a helpful AI assistant.";
    }

    /// <summary>
    /// Load prompt configuration from embedded JSON file
    /// </summary>
    private AgentPromptConfiguration LoadPromptConfiguration()
    {
        try
        {
            var assembly = typeof(AgentPromptService).Assembly;
            var resourceName = "FtoConsulting.PortfolioManager.Application.Configuration.AgentPrompts.json";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogWarning("Could not find embedded prompt configuration resource: {ResourceName}", resourceName);
                return CreateFallbackConfiguration();
            }
            
            using var reader = new StreamReader(stream);
            var jsonContent = reader.ReadToEnd();
            
            var config = JsonSerializer.Deserialize<AgentPromptConfiguration>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize prompt configuration, using fallback");
                return CreateFallbackConfiguration();
            }
            
            _logger.LogInformation("Successfully loaded agent prompt configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading prompt configuration, using fallback");
            return CreateFallbackConfiguration();
        }
    }

    /// <summary>
    /// Create a fallback configuration if loading fails
    /// </summary>
    private static AgentPromptConfiguration CreateFallbackConfiguration()
    {
        return new AgentPromptConfiguration
        {
            PortfolioAdvisor = new PortfolioAdvisorPromptConfig
            {
                BaseInstructions = "You are a friendly financial advisor helping the owner of Account ID {accountId}.",
                ToolUsageGuidance = new ToolUsageGuidanceConfig
                {
                    WhenToUseTools = new[] { "Show me my portfolio", "How am I doing?" },
                    WhenNotToUseTools = new[] { "General greetings", "Casual conversation" }
                },
                CommunicationStyle = new CommunicationStyleConfig
                {
                    Approach = "Be conversational and friendly.",
                    BadExample = new ExampleConfig
                    {
                        Title = "Bad Example",
                        Content = new string[] { "Dry bullet points" }
                    },
                    GoodExample = new ExampleConfig
                    {
                        Title = "Good Example", 
                        Content = "Friendly conversational approach."
                    }
                },
                FormattingGuidelines = new[] { "Use clear formatting", "Be consistent" },
                TableExample = new TableExampleConfig
                {
                    Description = "Use tables when appropriate",
                    Format = "| Column 1 | Column 2 |"
                },
                KeyReminders = new[] { "Be helpful", "Stay professional" },
                Personality = "Be the advisor they'd want to grab coffee with!"
            }
        };
    }
}

/// <summary>
/// Configuration classes for agent prompts
/// </summary>
public class AgentPromptConfiguration
{
    public PortfolioAdvisorPromptConfig PortfolioAdvisor { get; set; } = new();
}

public class PortfolioAdvisorPromptConfig
{
    public string BaseInstructions { get; set; } = string.Empty;
    public ToolUsageGuidanceConfig ToolUsageGuidance { get; set; } = new();
    public CommunicationStyleConfig CommunicationStyle { get; set; } = new();
    public string[] FormattingGuidelines { get; set; } = Array.Empty<string>();
    public TableExampleConfig TableExample { get; set; } = new();
    public string[] KeyReminders { get; set; } = Array.Empty<string>();
    public string Personality { get; set; } = string.Empty;
}

public class ToolUsageGuidanceConfig
{
    public string[] WhenToUseTools { get; set; } = Array.Empty<string>();
    public string[] WhenNotToUseTools { get; set; } = Array.Empty<string>();
}

public class CommunicationStyleConfig
{
    public string Approach { get; set; } = string.Empty;
    public ExampleConfig BadExample { get; set; } = new();
    public ExampleConfig GoodExample { get; set; } = new();
}

public class ExampleConfig
{
    public string Title { get; set; } = string.Empty;
    public object Content { get; set; } = string.Empty;
}

public class TableExampleConfig
{
    public string Description { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
}