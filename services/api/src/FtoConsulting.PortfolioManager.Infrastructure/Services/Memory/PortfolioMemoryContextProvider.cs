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
    protected override async ValueTask<AIContext> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instructions = new StringBuilder();
            
            // Load recent conversation summaries for context
            var beforeSummaries = instructions.Length;
            await LoadRecentSummariesAsync(instructions, cancellationToken);
            var summariesSize = instructions.Length - beforeSummaries;
            
            // Add user preferences if available
            var beforePreferences = instructions.Length;
            AddUserPreferences(instructions);
            var preferencesSize = instructions.Length - beforePreferences;
            
            // Add portfolio-specific context
            var beforePortfolio = instructions.Length;
            AddPortfolioContext(instructions);
            var portfolioSize = instructions.Length - beforePortfolio;

            // CONTEXT SIZE ANALYSIS: Log memory context breakdown
            var totalTokens = instructions.Length / 4; // Rough token estimation
            var summaryTokens = summariesSize / 4;
            var preferenceTokens = preferencesSize / 4;
            var portfolioTokens = portfolioSize / 4;
            
            _logger.LogInformation("Memory Context Analysis for account {AccountId}: Total={TotalTokens} tokens (Summaries={SummaryTokens}, Preferences={PreferenceTokens}, Portfolio={PortfolioTokens})", 
                _accountId, totalTokens, summaryTokens, preferenceTokens, portfolioTokens);

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
    protected override async ValueTask InvokedCoreAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Start background memory processing without blocking the response
            // Use Task.Run to execute on thread pool to avoid blocking the HTTP response
            _ = Task.Run(async () =>
            {
                try
                {
                    // Extract insights from the conversation
                    await ExtractConversationInsightsAsync(context, CancellationToken.None);
                    
                    // Update user preferences if patterns are detected
                    await UpdateUserPreferencesAsync(context, CancellationToken.None);

                    _logger.LogDebug("Completed background memory processing for account {AccountId}", _accountId);
                }
                catch (Exception backgroundEx)
                {
                    _logger.LogWarning(backgroundEx, "Background memory processing failed for account {AccountId}", _accountId);
                    // Don't throw - background failure shouldn't impact user experience
                }
            });

            _logger.LogDebug("Started background memory processing for account {AccountId}", _accountId);
            
            // Satisfy async requirement since this is an override method
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating memory after invocation for account {AccountId}", _accountId);
        }
    }

    /// <summary>
    /// Serialize the memory state for persistence
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions jsonSerializerOptions)
    {
        return JsonSerializer.SerializeToElement(_memoryState, jsonSerializerOptions);
    }

    /// <summary>
    /// Load recent conversation summaries using tiered memory approach with token budgeting
    /// </summary>
    private async Task LoadRecentSummariesAsync(StringBuilder instructions, CancellationToken cancellationToken)
    {
        const int maxMemoryTokens = 2000; // Token budget for memory context
        var currentTokens = 0;
        
        // TIER 1: Essential persistent facts (cold memory) - always include, minimal tokens
        await LoadEssentialFactsAsync(instructions, cancellationToken);
        currentTokens += (instructions.Length / 4); // Rough token estimate
        
        // TIER 2: Recent summaries (hot memory) - last 7 days, full detail
        var hotMemoryCutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var hotSummaries = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId && ms.SummaryDate >= hotMemoryCutoff)
            .OrderByDescending(ms => ms.SummaryDate)
            .Take(3) // Limit recent summaries
            .ToListAsync(cancellationToken);

        if (hotSummaries.Any() && currentTokens < maxMemoryTokens)
        {
            instructions.AppendLine("## Recent Conversations:");
            
            foreach (var summary in hotSummaries.OrderBy(s => s.SummaryDate))
            {
                var summaryText = $"**{summary.SummaryDate:MMM dd}:** {summary.Summary}";
                var summaryTokens = summaryText.Length / 4;
                
                if (currentTokens + summaryTokens > maxMemoryTokens)
                    break; // Stop if we hit token budget
                    
                instructions.AppendLine(summaryText);
                currentTokens += summaryTokens;
                
                // Add condensed key topics only
                if (!string.IsNullOrEmpty(summary.KeyTopics) && currentTokens < maxMemoryTokens - 100)
                {
                    try
                    {
                        var topicsText = ExtractCondensedTopics(summary.KeyTopics);
                        if (!string.IsNullOrEmpty(topicsText))
                        {
                            instructions.AppendLine(topicsText);
                            currentTokens += topicsText.Length / 4;
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
        
        // TIER 3: Warm memory (7-30 days) - compressed if budget allows
        if (currentTokens < maxMemoryTokens - 200)
        {
            await LoadWarmMemoryAsync(instructions, maxMemoryTokens - currentTokens, cancellationToken);
        }
    }

    /// <summary>
    /// Load only essential persistent facts (cold memory) with strict token limits
    /// </summary>
    private async Task LoadEssentialFactsAsync(StringBuilder instructions, CancellationToken cancellationToken)
    {
        // Only load the most recent persistent preferences from memory state and recent summaries
        var recentSummariesForFacts = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId)
            .OrderByDescending(ms => ms.SummaryDate)
            .Take(3) // Only look at last 3 summaries for essential facts
            .ToListAsync(cancellationToken);

        var essentialFacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var corePreferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var summary in recentSummariesForFacts)
        {
            // Extract only critical persistent facts (name, goals, etc.)
            if (!string.IsNullOrEmpty(summary.KeyTopics))
            {
                try
                {
                    if (summary.KeyTopics.TrimStart().StartsWith('{'))
                    {
                        var summaryData = JsonSerializer.Deserialize<JsonElement>(summary.KeyTopics);
                        
                        if (summaryData.TryGetProperty("importantFacts", out var factsArray))
                        {
                            var facts = JsonSerializer.Deserialize<string[]>(factsArray.GetRawText());
                            if (facts != null)
                            {
                                foreach (var fact in facts.Take(2)) // Max 2 facts per summary
                                {
                                    if (IsCriticalPersistentFact(fact))
                                    {
                                        essentialFacts.Add(fact);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse essential facts from summary {SummaryId}", summary.Id);
                }
            }

            // Extract core user preferences
            if (!string.IsNullOrEmpty(summary.UserPreferences))
            {
                try
                {
                    var preferences = JsonSerializer.Deserialize<Dictionary<string, string>>(summary.UserPreferences);
                    if (preferences != null)
                    {
                        foreach (var pref in preferences.Where(p => IsCorePreference(p.Key)))
                        {
                            corePreferences[pref.Key] = pref.Value;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse core preferences from summary {SummaryId}", summary.Id);
                }
            }
        }

        // Add essential information with strict limits
        if (essentialFacts.Any() || corePreferences.Any())
        {
            instructions.AppendLine("## Essential User Context:");
            
            if (essentialFacts.Any())
            {
                // Limit to top 3 essential facts
                foreach (var fact in essentialFacts.OrderBy(f => f).Take(3))
                {
                    instructions.AppendLine($"- {fact}");
                }
            }

            if (corePreferences.Any())
            {
                // Only show critical preferences
                foreach (var pref in corePreferences.OrderBy(p => p.Key))
                {
                    instructions.AppendLine($"- {pref.Key}: {pref.Value}");
                }
            }
            
            instructions.AppendLine();
        }
    }

    /// <summary>
    /// Load compressed warm memory (7-30 days old) with remaining token budget
    /// </summary>
    private async Task LoadWarmMemoryAsync(StringBuilder instructions, int remainingTokens, CancellationToken cancellationToken)
    {
        if (remainingTokens < 100) return; // Not enough budget
        
        var warmMemoryStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var warmMemoryEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        
        var warmSummaries = await _dbContext.MemorySummaries
            .Include(ms => ms.ConversationThread)
            .Where(ms => ms.ConversationThread.AccountId == _accountId && 
                        ms.SummaryDate >= warmMemoryStart && 
                        ms.SummaryDate < warmMemoryEnd)
            .OrderByDescending(ms => ms.SummaryDate)
            .Take(3) // Limit warm memory entries
            .ToListAsync(cancellationToken);

        if (warmSummaries.Any())
        {
            instructions.AppendLine("## Previous Context:");
            
            var usedTokens = 0;
            foreach (var summary in warmSummaries.OrderBy(s => s.SummaryDate))
            {
                // Compress to just key points
                var compressedSummary = CompressSummary(summary.Summary);
                var tokenEstimate = compressedSummary.Length / 4;
                
                if (usedTokens + tokenEstimate > remainingTokens - 50)
                    break;
                    
                instructions.AppendLine($"**{summary.SummaryDate:MMM dd}:** {compressedSummary}");
                usedTokens += tokenEstimate;
            }
            
            instructions.AppendLine();
        }
    }

    /// <summary>
    /// Determine if a fact is critical for persistent memory (only the most important)
    /// </summary>
    private static bool IsCriticalPersistentFact(string fact)
    {
        if (string.IsNullOrWhiteSpace(fact))
            return false;
            
        var lowerFact = fact.ToLowerInvariant();
        
        // Only critical identity and goal information
        var criticalIndicators = new[]
        {
            "name", "called", "i'm", "i am",
            "investment goal", "financial goal", "saving for", "retirement",
            "risk tolerance", "conservative investor", "aggressive investor"
        };

        return criticalIndicators.Any(indicator => lowerFact.Contains(indicator));
    }

    /// <summary>
    /// Determine if a preference is core/critical
    /// </summary>
    private static bool IsCorePreference(string preferenceKey)
    {
        var corePreferences = new[] { "RiskTolerance", "CommunicationStyle", "InvestmentGoal" };
        return corePreferences.Contains(preferenceKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract only essential topics from key topics JSON (condensed version)
    /// </summary>
    private static string ExtractCondensedTopics(string keyTopicsJson)
    {
        try
        {
            if (keyTopicsJson.TrimStart().StartsWith('{'))
            {
                var topicsData = JsonSerializer.Deserialize<JsonElement>(keyTopicsJson);
                
                if (topicsData.TryGetProperty("keyTopics", out var topicsArray))
                {
                    var topics = JsonSerializer.Deserialize<string[]>(topicsArray.GetRawText());
                    if (topics != null && topics.Length > 0)
                    {
                        // Only show top 3 topics
                        return $"Topics: {string.Join(", ", topics.Take(3))}";
                    }
                }
            }
            else
            {
                // Legacy format
                var topics = JsonSerializer.Deserialize<string[]>(keyTopicsJson);
                if (topics != null && topics.Length > 0)
                {
                    return $"Topics: {string.Join(", ", topics.Take(3))}";
                }
            }
        }
        catch
        {
            // Silently fail for condensed version
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Compress a summary to key points only
    /// </summary>
    private static string CompressSummary(string summary)
    {
        if (string.IsNullOrEmpty(summary) || summary.Length <= 100)
            return summary;
            
        // Simple compression: take first sentence and any sentences with key financial terms
        var sentences = summary.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var keyTerms = new[] { "portfolio", "investment", "stock", "fund", "risk", "return", "performance" };
        
        var compressedSentences = new List<string> { sentences[0] }; // Always include first sentence
        
        foreach (var sentence in sentences.Skip(1))
        {
            if (keyTerms.Any(term => sentence.ToLowerInvariant().Contains(term)))
            {
                compressedSentences.Add(sentence);
                if (compressedSentences.Count >= 2) break; // Limit to 2 additional sentences
            }
        }
        
        return string.Join(". ", compressedSentences) + ".";
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