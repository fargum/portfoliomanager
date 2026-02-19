using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System.Reflection;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of agent prompt service that loads prompts from JSON configuration
/// </summary>
public class AgentPromptService(ILogger<AgentPromptService> logger) : IAgentPromptService
{
    private AgentPromptConfiguration? _promptConfig;
    
    private AgentPromptConfiguration PromptConfig => _promptConfig ??= LoadPromptConfiguration();

    public string GetPortfolioAdvisorPrompt(int accountId)
    {
        try
        {
            var config = PromptConfig.PortfolioAdvisor;
            var promptBuilder = new StringBuilder();

            // Base instructions with account ID
            promptBuilder.AppendLine(config.BaseInstructions.Replace("{accountId}", accountId.ToString()));
            promptBuilder.AppendLine();

            // Tool usage guidance
            promptBuilder.AppendLine("WHEN TO USE YOUR TOOLS:");
            
            // Emit the critical rule first and prominently so all models see it
            if (!string.IsNullOrEmpty(config.ToolUsageGuidance.CriticalRule))
            {
                promptBuilder.AppendLine(config.ToolUsageGuidance.CriticalRule);
                promptBuilder.AppendLine();
            }
            
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
            logger.LogError(ex, "Error building portfolio advisor prompt for account {AccountId}", accountId);
            
            // Fallback to a basic prompt if configuration fails
            return $"You are a helpful financial advisor for Account ID {accountId}. Provide clear, friendly assistance with portfolio questions.";
        }
    }

    public string GetPrompt(string promptName, Dictionary<string, object>? parameters = null)
    {
        // Handle PortfolioAdvisor prompt
        if (promptName == "PortfolioAdvisor" && parameters?.ContainsKey("accountId") == true)
        {
            var accountId = Convert.ToInt32(parameters["accountId"]);
            return GetPortfolioAdvisorPrompt(accountId);
        }

        // Handle MemoryExtractionAgent prompt
        if (promptName == "MemoryExtractionAgent")
        {
            return GetMemoryExtractionPrompt(parameters);
        }

        logger.LogWarning("Unknown prompt name: {PromptName}", promptName);
        return "You are a helpful AI assistant.";
    }

    /// <summary>
    /// Get the memory extraction agent prompt
    /// </summary>
    /// <param name="parameters">Parameters for substitution (accountId, currentDate)</param>
    /// <returns>Complete prompt text for memory extraction</returns>
    private string GetMemoryExtractionPrompt(Dictionary<string, object>? parameters = null)
    {
        var config = LoadPromptConfiguration();
        
        // Get the memory extraction config
        if (!config.AdditionalAgents.ContainsKey("MemoryExtractionAgent"))
        {
            logger.LogError("MemoryExtractionAgent configuration not found in AgentPrompts.json");
            return "You are a helpful assistant that extracts information from conversations.";
        }

        var memoryConfigElement = config.AdditionalAgents["MemoryExtractionAgent"];
        
        // Convert JsonElement to formatted prompt string
        var promptBuilder = new StringBuilder();
        
        // Build a comprehensive prompt from the JSON structure
        if (memoryConfigElement.TryGetProperty("BaseInstructions", out var baseInstructions))
        {
            var instructions = baseInstructions.GetString() ?? "";
            
            // Apply parameter substitution
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    instructions = instructions.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? "");
                }
            }
            
            promptBuilder.AppendLine(instructions);
            promptBuilder.AppendLine();
        }

        // Add the critical output instruction
        if (memoryConfigElement.TryGetProperty("CRITICAL_OUTPUT_INSTRUCTION", out var criticalInstruction))
        {
            promptBuilder.AppendLine("CRITICAL OUTPUT REQUIREMENT:");
            promptBuilder.AppendLine(criticalInstruction.GetString());
            promptBuilder.AppendLine();
        }

        // Add the output format example
        if (memoryConfigElement.TryGetProperty("OutputFormat", out var outputFormat) && 
            outputFormat.TryGetProperty("Example", out var example))
        {
            promptBuilder.AppendLine("REQUIRED OUTPUT FORMAT (example):");
            promptBuilder.AppendLine(example.GetRawText());
        }

        return promptBuilder.ToString();
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
                logger.LogWarning("Could not find embedded prompt configuration resource: {ResourceName}", resourceName);
                return CreateFallbackConfiguration();
            }
            
            using var reader = new StreamReader(stream);           
            var jsonContent = reader.ReadToEnd();
            
            // Parse as JsonDocument first to handle dynamic structure
            using var document = JsonDocument.Parse(jsonContent);
            
            var config = new AgentPromptConfiguration();
            
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name == "PortfolioAdvisor")
                {
                    // Handle PortfolioAdvisor with strongly typed deserialization
                    config.PortfolioAdvisor = JsonSerializer.Deserialize<PortfolioAdvisorPromptConfig>(
                        property.Value.GetRawText(), 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                else
                {
                    // Handle other agents dynamically
                    config.AdditionalAgents[property.Name] = property.Value.Clone();
                }
            }
            
            logger.LogInformation("Successfully loaded agent prompt configuration with {AgentCount} agents", 
                config.AdditionalAgents.Count + 1);
            return config;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading prompt configuration, using fallback");          
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
    public Dictionary<string, JsonElement> AdditionalAgents { get; set; } = new();
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
    public string CriticalRule { get; set; } = string.Empty;
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
