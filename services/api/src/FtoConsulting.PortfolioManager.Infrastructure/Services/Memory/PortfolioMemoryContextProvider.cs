using System.Text;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Domain.Entities;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FtoConsulting.PortfolioManager.Infrastructure.Services.Memory;

/// <summary>
/// Memory context provider for portfolio management AI agents
/// Provides context from conversation summaries and user preferences
/// </summary>
public class PortfolioMemoryContextProvider : AIContextProvider
{
    private readonly PortfolioManagerDbContext _dbContext;
    private readonly IChatClient _chatClient;
    private readonly ILogger<PortfolioMemoryContextProvider> _logger;
    private readonly int _accountId;
    private PortfolioMemoryState _memoryState;

    public PortfolioMemoryContextProvider(
        PortfolioManagerDbContext dbContext,
        IChatClient chatClient,
        ILogger<PortfolioMemoryContextProvider> logger,
        int accountId,
        PortfolioMemoryState? memoryState = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountId = accountId;
        _memoryState = memoryState ?? new PortfolioMemoryState();
    }

    public PortfolioMemoryContextProvider(
        PortfolioManagerDbContext dbContext,
        IChatClient chatClient,
        ILogger<PortfolioMemoryContextProvider> logger,
        int accountId,
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountId = accountId;
        
        _memoryState = serializedState.ValueKind == JsonValueKind.Object
            ? serializedState.Deserialize<PortfolioMemoryState>(jsonSerializerOptions)!
            : new PortfolioMemoryState();
    }

    /// <summary>
    /// Called before each AI agent invocation to provide memory context
    /// </summary>
    public override async ValueTask<AIContext> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instructions = new StringBuilder();
            
            // Load recent conversation summaries for context
            await LoadRecentSummariesAsync(instructions, cancellationToken);
            
            // Add user preferences if available
            AddUserPreferences(instructions);
            
            // Add portfolio-specific context
            AddPortfolioContext(instructions);

            _logger.LogDebug("Generated memory context for account {AccountId}: {Instructions}", 
                _accountId, instructions.ToString());

            return new AIContext
            {
                Instructions = instructions.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating memory context for account {AccountId}", _accountId);
            return new AIContext { Instructions = string.Empty };
        }
    }

    /// <summary>
    /// Called after each AI agent invocation to update memory
    /// </summary>
    public override async ValueTask InvokedAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract insights from the conversation
            await ExtractConversationInsightsAsync(context, cancellationToken);
            
            // Update user preferences if patterns are detected
            await UpdateUserPreferencesAsync(context, cancellationToken);

            _logger.LogDebug("Updated memory state after invocation for account {AccountId}", _accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating memory after invocation for account {AccountId}", _accountId);
        }
    }

    /// <summary>
    /// Serialize the memory state for persistence
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(_memoryState, jsonSerializerOptions);
    }

    /// <summary>
    /// Load recent conversation summaries to provide context
    /// </summary>
    private async Task LoadRecentSummariesAsync(StringBuilder instructions, CancellationToken cancellationToken)
    {
        // Load consolidated long-term memories first
        await LoadConsolidatedMemoriesAsync(instructions, cancellationToken);
        
        // Then load recent summaries for immediate context
        var recentSummaries = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId)
            .OrderByDescending(ms => ms.SummaryDate)
            .Take(5) // Recent context only
            .ToListAsync(cancellationToken);

        if (recentSummaries.Any())
        {
            instructions.AppendLine("## Recent Conversation Context:");
            
            foreach (var summary in recentSummaries.OrderBy(s => s.SummaryDate))
            {
                instructions.AppendLine($"**{summary.SummaryDate:MMM dd}:** {summary.Summary}");
                
                // Add key topics if available
                if (!string.IsNullOrEmpty(summary.KeyTopics))
                {
                    try
                    {
                        // Try parsing as new format first (object with keyTopics property)
                        string[]? topics = null;
                        string[]? importantFacts = null;
                        
                        if (summary.KeyTopics.TrimStart().StartsWith('{'))
                        {
                            // New format: {"keyTopics": ["topic1", "topic2"], "importantFacts": [...], "confidenceScore": 0.85}
                            var keyTopicsObject = JsonSerializer.Deserialize<JsonElement>(summary.KeyTopics);
                            
                            if (keyTopicsObject.TryGetProperty("keyTopics", out var keyTopicsArray))
                            {
                                topics = JsonSerializer.Deserialize<string[]>(keyTopicsArray.GetRawText());
                            }
                            
                            if (keyTopicsObject.TryGetProperty("importantFacts", out var importantFactsArray))
                            {
                                importantFacts = JsonSerializer.Deserialize<string[]>(importantFactsArray.GetRawText());
                            }
                        }
                        else
                        {
                            // Legacy format: ["topic1", "topic2"]
                            topics = JsonSerializer.Deserialize<string[]>(summary.KeyTopics);
                        }
                        
                        if (topics?.Length > 0)
                        {
                            instructions.AppendLine($"Key topics: {string.Join(", ", topics)}");
                        }
                        
                        if (importantFacts?.Length > 0)
                        {
                            instructions.AppendLine($"Important facts: {string.Join("; ", importantFacts)}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse key topics for summary {SummaryId}", summary.Id);
                    }
                }
            }
            
            instructions.AppendLine();
        }
    }

    /// <summary>
    /// Load and consolidate long-term memories that should persist across all conversations
    /// </summary>
    private async Task LoadConsolidatedMemoriesAsync(StringBuilder instructions, CancellationToken cancellationToken)
    {
        // Get ALL summaries for this account to extract persistent information
        var allSummaries = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId)
            .OrderBy(ms => ms.SummaryDate) // Chronological order for proper precedence
            .ToListAsync(cancellationToken);

        if (!allSummaries.Any())
            return;

        // Consolidate core facts and preferences from all summaries
        var consolidatedFacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consolidatedPreferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var persistentTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var summary in allSummaries)
        {
            // Extract important facts
            if (!string.IsNullOrEmpty(summary.KeyTopics))
            {
                try
                {
                    if (summary.KeyTopics.TrimStart().StartsWith('{'))
                    {
                        var summaryData = JsonSerializer.Deserialize<JsonElement>(summary.KeyTopics);
                        
                        // Extract important facts
                        if (summaryData.TryGetProperty("importantFacts", out var factsArray))
                        {
                            var facts = JsonSerializer.Deserialize<string[]>(factsArray.GetRawText());
                            if (facts != null)
                            {
                                foreach (var fact in facts)
                                {
                                    if (IsPersistentFact(fact))
                                    {
                                        consolidatedFacts.Add(fact);
                                    }
                                }
                            }
                        }
                        
                        // Extract persistent topics
                        if (summaryData.TryGetProperty("keyTopics", out var topicsArray))
                        {
                            var topics = JsonSerializer.Deserialize<string[]>(topicsArray.GetRawText());
                            if (topics != null)
                            {
                                foreach (var topic in topics)
                                {
                                    persistentTopics.Add(topic);
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse summary data for consolidation from summary {SummaryId}", summary.Id);
                }
            }

            // Extract user preferences
            if (!string.IsNullOrEmpty(summary.UserPreferences))
            {
                try
                {
                    var preferences = JsonSerializer.Deserialize<Dictionary<string, string>>(summary.UserPreferences);
                    if (preferences != null)
                    {
                        foreach (var pref in preferences)
                        {
                            // Later preferences override earlier ones
                            consolidatedPreferences[pref.Key] = pref.Value;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse user preferences from summary {SummaryId}", summary.Id);
                }
            }
        }

        // Add consolidated information to instructions
        if (consolidatedFacts.Any() || consolidatedPreferences.Any())
        {
            instructions.AppendLine("## Long-term User Profile:");
            
            if (consolidatedFacts.Any())
            {
                instructions.AppendLine("**Core Facts About This User:**");
                foreach (var fact in consolidatedFacts.OrderBy(f => f))
                {
                    instructions.AppendLine($"- {fact}");
                }
                instructions.AppendLine();
            }

            if (consolidatedPreferences.Any())
            {
                instructions.AppendLine("**User Preferences:**");
                foreach (var pref in consolidatedPreferences.OrderBy(p => p.Key))
                {
                    instructions.AppendLine($"- {pref.Key}: {pref.Value}");
                }
                instructions.AppendLine();
            }

            if (persistentTopics.Any())
            {
                instructions.AppendLine($"**Recurring Topics:** {string.Join(", ", persistentTopics.OrderBy(t => t))}");
                instructions.AppendLine();
            }
        }
    }

    /// <summary>
    /// Determine if a fact should be considered persistent (long-term memory)
    /// </summary>
    private static bool IsPersistentFact(string fact)
    {
        if (string.IsNullOrWhiteSpace(fact))
            return false;
            
        var lowerFact = fact.ToLowerInvariant();
        
        // Personal identity information
        var persistentIndicators = new[]
        {
            "name", "called", "i'm", "i am", "my wife", "my husband", "my partner",
            "my children", "my kids", "my daughter", "my son", "my family",
            "work in", "work for", "job", "career", "profession", "retired",
            "age", "years old", "born in", "live in", "from", "originally",
            "investment goal", "financial goal", "saving for", "planning for",
            "risk tolerance", "conservative", "aggressive", "moderate risk",
            "time horizon", "retirement", "university", "college", "education"
        };

        return persistentIndicators.Any(indicator => lowerFact.Contains(indicator));
    }

    /// <summary>
    /// Add user preferences to the context
    /// </summary>
    private void AddUserPreferences(StringBuilder instructions)
    {
        if (_memoryState.UserPreferences.Count > 0)
        {
            instructions.AppendLine("## User Preferences:");
            
            foreach (var preference in _memoryState.UserPreferences)
            {
                instructions.AppendLine($"- **{preference.Key}:** {preference.Value}");
            }
            
            instructions.AppendLine();
        }
    }

    /// <summary>
    /// Add portfolio-specific context
    /// </summary>
    private void AddPortfolioContext(StringBuilder instructions)
    {
        instructions.AppendLine("## Portfolio Analysis Guidelines:");
        instructions.AppendLine("- Always use real portfolio data via the available tools");
        instructions.AppendLine("- Format currency as £1,234.56 (GBP, not USD)");
        instructions.AppendLine("- Use UK date formats (DD/MM/YYYY or DD MMM YYYY)");
        instructions.AppendLine("- Provide actionable insights based on market analysis");
        instructions.AppendLine("- Reference previous conversations when relevant");
        instructions.AppendLine();
    }

    /// <summary>
    /// Extract insights from the conversation for future reference
    /// </summary>
    private async Task ExtractConversationInsightsAsync(InvokedContext context, CancellationToken cancellationToken)
    {
        try
        {
            var userMessages = context.RequestMessages
                .Where(m => m.Role == ChatRole.User)
                .Select(m => m.Text ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (!userMessages.Any()) return;

            var conversationText = string.Join(" ", userMessages);
            
            // Use AI to extract insights
            var extractionPrompt = $@"
Analyze this portfolio-related conversation and extract:
1. User investment preferences or goals
2. Areas of concern or interest
3. Specific financial instruments mentioned
4. Risk tolerance indicators

Conversation: {conversationText}

Respond in JSON format:
{{
    ""preferences"": {{""key"": ""value""}},
    ""concerns"": [""concern1"", ""concern2""],
    ""instruments"": [""AAPL"", ""MSFT""],
    ""riskTolerance"": ""conservative|moderate|aggressive""
}}";

            try
            {
            var response = await _chatClient.GetResponseAsync(
                [new AIChatMessage(ChatRole.User, extractionPrompt)],
                new ChatOptions(), // Remove temperature - use model default
                cancellationToken);                if (!string.IsNullOrEmpty(response.Text))
                {
                    var insights = JsonSerializer.Deserialize<ConversationInsights>(response.Text);
                    if (insights != null)
                    {
                        // Update memory state with extracted insights
                        foreach (var preference in insights.Preferences ?? new Dictionary<string, string>())
                        {
                            _memoryState.UserPreferences[preference.Key] = preference.Value;
                        }

                        if (!string.IsNullOrEmpty(insights.RiskTolerance))
                        {
                            _memoryState.UserPreferences["RiskTolerance"] = insights.RiskTolerance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract conversation insights for account {AccountId}", _accountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExtractConversationInsightsAsync for account {AccountId}", _accountId);
        }
    }

    /// <summary>
    /// Update user preferences based on conversation patterns
    /// </summary>
    private async Task UpdateUserPreferencesAsync(InvokedContext context, CancellationToken cancellationToken)
    {
        // Simple preference learning based on question patterns
        var userMessages = context.RequestMessages
            .Where(m => m.Role == ChatRole.User)
            .Select(m => m.Text?.ToLowerInvariant() ?? string.Empty)
            .ToList();

        foreach (var message in userMessages)
        {
            // Detect communication preferences
            if (message.Contains("detailed") || message.Contains("comprehensive"))
            {
                _memoryState.UserPreferences["CommunicationStyle"] = "Detailed";
            }
            else if (message.Contains("brief") || message.Contains("summary"))
            {
                _memoryState.UserPreferences["CommunicationStyle"] = "Brief";
            }

            // Detect analysis preferences
            if (message.Contains("risk") || message.Contains("safe"))
            {
                _memoryState.UserPreferences["FocusArea"] = "Risk Analysis";
            }
            else if (message.Contains("performance") || message.Contains("return"))
            {
                _memoryState.UserPreferences["FocusArea"] = "Performance Analysis";
            }
            else if (message.Contains("news") || message.Contains("market"))
            {
                _memoryState.UserPreferences["FocusArea"] = "Market Intelligence";
            }
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// State object for portfolio memory context
/// </summary>
public class PortfolioMemoryState
{
    public Dictionary<string, string> UserPreferences { get; set; } = new();
    public Dictionary<string, object> ConversationMetrics { get; set; } = new();
}

/// <summary>
/// Structure for AI-extracted conversation insights
/// </summary>
public class ConversationInsights
{
    public Dictionary<string, string>? Preferences { get; set; }
    public string[]? Concerns { get; set; }
    public string[]? Instruments { get; set; }
    public string? RiskTolerance { get; set; }
}