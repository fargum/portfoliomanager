using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using DomainChatMessage = FtoConsulting.PortfolioManager.Domain.Entities.ChatMessage;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Services.Memory;

/// <summary>
/// AI-powered service for extracting meaningful information from conversations
/// </summary>
public class MemoryExtractionService(
    ILogger<MemoryExtractionService> logger,
    IAiChatService aiChatService,
    IAgentPromptService promptService) : IMemoryExtractionService
{
    private const int MaxRecentAssistantMessages = 5;
    private const int MaxAssistantMessageLength = 200;
    private const string MemoryExtractionPromptName = "MemoryExtractionAgent";

    /// <summary>
    /// Extract meaningful memories from a conversation thread using AI
    /// </summary>
    public async Task<MemoryExtractionResult> ExtractMemoriesFromConversationAsync(
        IEnumerable<DomainChatMessage> messages, 
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        if (!messageList.Any())
        {
            logger.LogDebug("No messages provided for memory extraction");
            return MemoryExtractionResult.Empty();
        }

        logger.LogInformation("Processing {MessageCount} messages for memory extraction for account {AccountId}", 
            messageList.Count, accountId);

        // Create conversation text for AI analysis
        var conversationText = FormatConversationForAnalysis(messageList);
        
        // Get AI-powered memory extraction
        var extractionResult = await PerformAIMemoryExtractionAsync(conversationText, accountId, cancellationToken);
        
        logger.LogInformation("Successfully extracted {FactCount} facts and {PreferenceCount} preferences from conversation with {MessageCount} messages",
            extractionResult.ImportantFacts.Count, 
            extractionResult.UserPreferences.Count,
            messageList.Count);

        return extractionResult;
    }    /// <summary>
    /// Use AI to analyze conversation and extract structured memory information
    /// </summary>
    private async Task<MemoryExtractionResult> PerformAIMemoryExtractionAsync(
        string conversationText, 
        int accountId, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the memory extraction prompt
            var systemPrompt = promptService.GetPrompt(
                MemoryExtractionPromptName, 
                new Dictionary<string, object>
                {
                    ["accountId"] = accountId,
                    ["currentDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
                });

            // Use OpenAI.Chat.ChatMessage types (compatible with existing service)
            var messages = new OpenAI.Chat.ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Please analyze this conversation and extract important memories:\n\n{conversationText}")
            };

            var response = await aiChatService.CompleteChatAsync(messages, cancellationToken);

            // Parse the JSON response
            if (string.IsNullOrEmpty(response))
            {
                throw new InvalidOperationException("AI returned empty response for memory extraction");
            }

            // Extract JSON from response (AI might wrap it in text)
            var jsonContent = ExtractJsonFromResponse(response);

            var extractionData = JsonSerializer.Deserialize<MemoryExtractionData>(jsonContent);
            if (extractionData == null)
            {
                throw new InvalidOperationException("Failed to parse AI memory extraction response");
            }

            return new MemoryExtractionResult
            {
                ImportantFacts = extractionData.ImportantFacts ?? new List<string>(),
                UserPreferences = extractionData.UserPreferences ?? new Dictionary<string, string>(),
                KeyTopics = extractionData.KeyTopics ?? new List<string>(),
                Summary = extractionData.Summary ?? "AI conversation analysis",
                ConfidenceScore = extractionData.ConfidenceScore
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AI memory extraction for account {AccountId}", accountId);
            throw;
        }
    }

    /// <summary>
    /// Extract JSON content from AI response that might contain additional text
    /// </summary>
    private string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        // Try to find JSON content within the response
        var openBrace = response.IndexOf('{');
        var closeBrace = response.LastIndexOf('}');

        if (openBrace != -1 && closeBrace != -1 && closeBrace > openBrace)
        {
            var jsonContent = response.Substring(openBrace, closeBrace - openBrace + 1);
            
            // Validate that it's proper JSON by counting braces
            int braceCount = 0;
            for (int i = 0; i < jsonContent.Length; i++)
            {
                if (jsonContent[i] == '{') braceCount++;
                else if (jsonContent[i] == '}') braceCount--;
            }

            // If braces are balanced, return this content
            if (braceCount == 0)
                return jsonContent;
        }

        // If we can't extract JSON, return the original response and let JSON parser handle the error
        return response;
    }

    /// <summary>
    /// Format conversation messages for AI analysis - prioritize user messages
    /// </summary>
    private static string FormatConversationForAnalysis(List<DomainChatMessage> messages)
    {
        var conversationBuilder = new System.Text.StringBuilder();
        
        // Prioritize user messages as they contain the most important personal information
        var userMessages = messages
            .Where(m => m.Role.ToUpperInvariant() == "USER")
            .OrderBy(m => m.MessageTimestamp)
            .ToList();

        // Include only the most recent assistant messages (last 5) to provide context
        var recentAssistantMessages = messages
            .Where(m => m.Role.ToUpperInvariant() == "ASSISTANT")
            .OrderByDescending(m => m.MessageTimestamp)
            .Take(MaxRecentAssistantMessages)
            .OrderBy(m => m.MessageTimestamp)
            .ToList();

        // Combine and sort by timestamp
        var relevantMessages = userMessages
            .Concat(recentAssistantMessages)
            .OrderBy(m => m.MessageTimestamp)
            .ToList();

        conversationBuilder.AppendLine("=== CONVERSATION SUMMARY FOR MEMORY EXTRACTION ===");
        conversationBuilder.AppendLine($"Total messages analyzed: {messages.Count}");
        conversationBuilder.AppendLine($"User messages: {userMessages.Count}");
        conversationBuilder.AppendLine($"Recent assistant responses included: {recentAssistantMessages.Count}");
        conversationBuilder.AppendLine();
        conversationBuilder.AppendLine("=== KEY USER INPUTS AND RECENT CONTEXT ===");
        conversationBuilder.AppendLine();
        
        foreach (var message in relevantMessages)
        {
            var role = message.Role.ToUpperInvariant() == "USER" ? "USER" : "ASSISTANT";
            var timestamp = message.MessageTimestamp.ToString("HH:mm:ss");
            
            // For user messages, include full content
            // For assistant messages, limit length to avoid tool call verbosity
            var content = role == "USER" 
                ? message.Content 
                : TruncateAssistantMessage(message.Content);
            
            conversationBuilder.AppendLine($"[{timestamp}] {role}: {content}");
            conversationBuilder.AppendLine();
        }

        conversationBuilder.AppendLine("=== FOCUS ON USER PREFERENCES, PERSONAL INFO, AND GOALS ===");

        return conversationBuilder.ToString();
    }

    /// <summary>
    /// Truncate assistant messages to avoid overwhelming the AI with tool call details
    /// </summary>
    private static string TruncateAssistantMessage(string content)
    {
        // If the message is short, return as-is
        if (content.Length <= MaxAssistantMessageLength)
            return content;
        
        // If it's long, it's likely a tool call or verbose response - truncate intelligently
        var truncated = content.Substring(0, MaxAssistantMessageLength);
        
        // Try to end at a sentence boundary
        var lastPeriod = truncated.LastIndexOf('.');
        var lastQuestion = truncated.LastIndexOf('?');
        var lastExclamation = truncated.LastIndexOf('!');
        
        var lastSentenceEnd = Math.Max(Math.Max(lastPeriod, lastQuestion), lastExclamation);
        
        if (lastSentenceEnd > MaxAssistantMessageLength / 2) // If we found a good break point
        {
            return content.Substring(0, lastSentenceEnd + 1) + " [...]";
        }
        
        return truncated + "...";
    }

}

/// <summary>
/// Service interface for AI-powered memory extraction
/// </summary>
public interface IMemoryExtractionService
{
    Task<MemoryExtractionResult> ExtractMemoriesFromConversationAsync(
        IEnumerable<DomainChatMessage> messages, 
        int accountId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of AI memory extraction
/// </summary>
public class MemoryExtractionResult
{
    public List<string> ImportantFacts { get; set; } = new();
    public Dictionary<string, string> UserPreferences { get; set; } = new();
    public List<string> KeyTopics { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public float ConfidenceScore { get; set; }

    public static MemoryExtractionResult Empty() => new()
    {
        Summary = "No memories extracted",
        ConfidenceScore = 0f
    };
}

/// <summary>
/// JSON structure for AI memory extraction response
/// </summary>
public class MemoryExtractionData
{
    public List<string> ImportantFacts { get; set; } = new();
    public Dictionary<string, string> UserPreferences { get; set; } = new();
    public List<string> KeyTopics { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public float ConfidenceScore { get; set; }
}