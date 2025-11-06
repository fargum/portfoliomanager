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
        var recentSummaries = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId)
            .OrderByDescending(ms => ms.SummaryDate)
            .Take(7) // Last week of summaries
            .ToListAsync(cancellationToken);

        if (recentSummaries.Any())
        {
            instructions.AppendLine("## Recent Conversation Context:");
            
            foreach (var summary in recentSummaries)
            {
                instructions.AppendLine($"**{summary.SummaryDate:MMM dd}:** {summary.Summary}");
                
                // Add key topics if available
                if (!string.IsNullOrEmpty(summary.KeyTopics))
                {
                    try
                    {
                        var topics = JsonSerializer.Deserialize<string[]>(summary.KeyTopics);
                        if (topics?.Length > 0)
                        {
                            instructions.AppendLine($"Key topics: {string.Join(", ", topics)}");
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