using System.Text.Json;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DomainChatMessage = FtoConsulting.PortfolioManager.Domain.Entities.ChatMessage;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FtoConsulting.PortfolioManager.Infrastructure.Services.Memory;

/// <summary>
/// PostgreSQL-based implementation of ChatHistoryProvider for the Microsoft Agent Framework
/// Provides persistent storage for conversation messages scoped to accounts
/// </summary>
public class PostgreSqlChatMessageStore : ChatHistoryProvider, ITokenAwareChatMessageStore
{
    private readonly PortfolioManagerDbContext _dbContext;
    private readonly ILogger<PostgreSqlChatMessageStore> _logger;
    private readonly int _accountId;
    private int? _conversationThreadId;

    public PostgreSqlChatMessageStore(
        PortfolioManagerDbContext dbContext,
        ILogger<PostgreSqlChatMessageStore> logger,
        int accountId,
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountId = accountId;

        // Deserialize the conversation thread ID if provided
        if (serializedStoreState.ValueKind is JsonValueKind.Number)
        {
            _conversationThreadId = serializedStoreState.GetInt32();
        }
    }

    /// <summary>
    /// Called after agent invocation to persist new messages to the conversation thread
    /// </summary>
    protected override async ValueTask InvokedCoreAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Combine request and response messages for persistence
            var messages = new List<AIChatMessage>();
            if (context.RequestMessages != null) messages.AddRange(context.RequestMessages);
            if (context.ResponseMessages != null) messages.AddRange(context.ResponseMessages);

            // Ensure we have a conversation thread
            await EnsureConversationThreadAsync(cancellationToken);

            var chatMessages = new List<Domain.Entities.ChatMessage>();

            foreach (var message in messages)
            {
                var metadata = new
                {
                    MessageId = message.MessageId,
                    AdditionalProperties = message.AdditionalProperties,
                    Contents = message.Contents?.Select(c => new
                    {
                        Type = c.GetType().Name,
                        Text = c is TextContent textContent ? textContent.Text : null,
                        Data = c.ToString()
                    })
                };

                var chatMessage = new DomainChatMessage(
                    conversationThreadId: _conversationThreadId!.Value,
                    role: message.Role.Value,
                    content: GetMessageText(message),
                    tokenCount: EstimateTokenCount(message),
                    metadata: JsonSerializer.Serialize(metadata)
                );

                chatMessages.Add(chatMessage);
            }

            _dbContext.ChatMessages.AddRange(chatMessages);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update thread activity
            await UpdateThreadActivityAsync(cancellationToken);

            _logger.LogInformation("Added {MessageCount} messages to conversation thread {ThreadId} for account {AccountId}",
                chatMessages.Count, _conversationThreadId, _accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding messages to conversation thread {ThreadId} for account {AccountId}",
                _conversationThreadId, _accountId);
            throw;
        }
    }

    /// <summary>
    /// Called before agent invocation to provide conversation history
    /// </summary>
    protected override async ValueTask<IEnumerable<AIChatMessage>> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_conversationThreadId == null)
            {
                _logger.LogDebug("No conversation thread ID set for account {AccountId}, returning empty messages", _accountId);
                return [];
            }

            // Get recent messages (last 50 to respect context window)
            var dbMessages = await _dbContext.ChatMessages
                .Where(cm => cm.ConversationThreadId == _conversationThreadId.Value)
                .OrderByDescending(cm => cm.MessageTimestamp)
                .Take(50)
                .OrderBy(cm => cm.MessageTimestamp)
                .ToListAsync(cancellationToken);

            var chatMessages = new List<AIChatMessage>();

            foreach (var dbMessage in dbMessages)
            {
                try
                {
                    var role = new ChatRole(dbMessage.Role);
                    var content = new TextContent(dbMessage.Content);
                    
                    var chatMessage = new AIChatMessage(role, [content]);
                    
                    // Restore message ID from metadata if available
                    if (!string.IsNullOrEmpty(dbMessage.Metadata))
                    {
                        try
                        {
                            var metadata = JsonSerializer.Deserialize<JsonElement>(dbMessage.Metadata);
                            if (metadata.TryGetProperty("MessageId", out var messageIdElement))
                            {
                                var messageId = messageIdElement.GetString();
                                if (!string.IsNullOrEmpty(messageId))
                                {
                                    chatMessage.MessageId = messageId;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse metadata for message {MessageId}", dbMessage.Id);
                        }
                    }

                    chatMessages.Add(chatMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message {MessageId}, skipping", dbMessage.Id);
                }
            }

            _logger.LogDebug("Retrieved {MessageCount} messages from conversation thread {ThreadId} for account {AccountId}",
                chatMessages.Count, _conversationThreadId, _accountId);

            return chatMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages from conversation thread {ThreadId} for account {AccountId}",
                _conversationThreadId, _accountId);
            throw;
        }
    }

    /// <summary>
    /// Serialize the store state for persistence
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions jsonSerializerOptions)
    {
        // Serialize the conversation thread ID for restoration
        return JsonSerializer.SerializeToElement(_conversationThreadId);
    }

    /// <summary>
    /// Ensure we have a conversation thread, creating one if necessary
    /// </summary>
    private async Task EnsureConversationThreadAsync(CancellationToken cancellationToken)
    {
        if (_conversationThreadId.HasValue)
        {
            return;
        }

        // Look for an active thread for this account
        var activeThread = await _dbContext.ConversationThreads
            .Where(ct => ct.AccountId == _accountId && ct.IsActive)
            .OrderByDescending(ct => ct.LastActivity)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeThread != null)
        {
            _conversationThreadId = activeThread.Id;
            _logger.LogDebug("Using existing active conversation thread {ThreadId} for account {AccountId}",
                _conversationThreadId, _accountId);
        }
        else
        {
            // Create a new conversation thread
            var newThread = new ConversationThread(
                accountId: _accountId,
                threadTitle: $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            );

            _dbContext.ConversationThreads.Add(newThread);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _conversationThreadId = newThread.Id;
            _logger.LogInformation("Created new conversation thread {ThreadId} for account {AccountId}",
                _conversationThreadId, _accountId);
        }
    }

    /// <summary>
    /// Update the thread's last activity timestamp
    /// </summary>
    private async Task UpdateThreadActivityAsync(CancellationToken cancellationToken)
    {
        if (!_conversationThreadId.HasValue)
        {
            return;
        }

        var thread = await _dbContext.ConversationThreads
            .FirstOrDefaultAsync(ct => ct.Id == _conversationThreadId.Value, cancellationToken);

        if (thread != null)
        {
            thread.UpdateActivity();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get messages within a specified token budget, prioritizing recent messages
    /// </summary>
    public async Task<IEnumerable<AIChatMessage>> GetMessagesWithinTokenBudgetAsync(
        int tokenBudget = 2000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_conversationThreadId == null)
            {
                _logger.LogDebug("No conversation thread ID set for account {AccountId}, returning empty messages", _accountId);
                return [];
            }

            // Get recent messages ordered by timestamp (most recent first)
            var dbMessages = await _dbContext.ChatMessages
                .Where(cm => cm.ConversationThreadId == _conversationThreadId.Value)
                .OrderByDescending(cm => cm.MessageTimestamp)
                .Take(100) // Get more messages than we need to work with
                .ToListAsync(cancellationToken);

            var selectedMessages = new List<AIChatMessage>();
            var currentTokenCount = 0;

            // Work backwards from most recent, adding messages until we hit budget
            foreach (var dbMessage in dbMessages)
            {
                var estimatedTokens = dbMessage.TokenCount > 0 ? dbMessage.TokenCount : EstimateTokenCount(dbMessage.Content);
                
                // Check if adding this message would exceed budget
                if (currentTokenCount + estimatedTokens > tokenBudget && selectedMessages.Any())
                {
                    break;
                }

                try
                {
                    var role = new ChatRole(dbMessage.Role);
                    var content = new TextContent(dbMessage.Content);
                    var chatMessage = new AIChatMessage(role, [content]);
                    
                    // Restore message ID from metadata if available
                    if (!string.IsNullOrEmpty(dbMessage.Metadata))
                    {
                        try
                        {
                            var metadata = JsonSerializer.Deserialize<JsonElement>(dbMessage.Metadata);
                            if (metadata.TryGetProperty("MessageId", out var messageIdElement))
                            {
                                var messageId = messageIdElement.GetString();
                                if (!string.IsNullOrEmpty(messageId))
                                {
                                    chatMessage.MessageId = messageId;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse metadata for message {MessageId}", dbMessage.Id);
                        }
                    }

                    selectedMessages.Add(chatMessage);
                    currentTokenCount += estimatedTokens;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message {MessageId}, skipping", dbMessage.Id);
                }
            }

            // Reverse to get chronological order (oldest first)
            selectedMessages.Reverse();

            _logger.LogDebug("Selected {MessageCount} messages ({TokenCount} tokens) from conversation thread {ThreadId} for account {AccountId}",
                selectedMessages.Count, currentTokenCount, _conversationThreadId, _accountId);

            return selectedMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving token-budgeted messages from conversation thread {ThreadId} for account {AccountId}",
                _conversationThreadId, _accountId);
            throw;
        }
    }

    /// <summary>
    /// Extract text content from a chat message
    /// </summary>
    private static string GetMessageText(AIChatMessage message)
    {
        if (message.Contents?.FirstOrDefault() is TextContent textContent)
        {
            return textContent.Text ?? string.Empty;
        }

        return message.Text ?? string.Empty;
    }

    /// <summary>
    /// Estimate token count for text content
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token
        return Math.Max(1, text.Length / 4);
    }

    /// <summary>
    /// Estimate token count for a message (rough approximation)
    /// </summary>
    private static int EstimateTokenCount(AIChatMessage message)
    {
        var text = GetMessageText(message);
        // Rough estimation: ~4 characters per token
        return Math.Max(1, text.Length / 4);
    }
}