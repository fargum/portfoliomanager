using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Memory;

/// <summary>
/// Implementation of conversation thread service
/// </summary>
public class ConversationThreadService : IConversationThreadService
{
    private readonly IConversationThreadRepository _threadRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IMemorySummaryRepository _summaryRepository;
    private readonly ILogger<ConversationThreadService> _logger;

    public ConversationThreadService(
        IConversationThreadRepository threadRepository,
        IChatMessageRepository messageRepository,
        IMemorySummaryRepository summaryRepository,
        ILogger<ConversationThreadService> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get or create an active conversation thread for the account
    /// </summary>
    public async Task<ConversationThread> GetOrCreateActiveThreadAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Look for the most recent active thread
            var activeThread = await _threadRepository.GetMostRecentActiveThreadAsync(accountId, cancellationToken);

            if (activeThread != null)
            {
                _logger.LogDebug("Found existing active thread {ThreadId} for account {AccountId}", 
                    activeThread.Id, accountId);
                return activeThread;
            }

            // Create a new thread if none exists
            return await CreateNewThreadAsync(accountId, $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating active thread for account {AccountId}", accountId);
            throw;
        }
    }

    /// <summary>
    /// Get a specific thread by ID, ensuring it belongs to the account
    /// </summary>
    public async Task<ConversationThread?> GetThreadByIdAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

            if (thread == null)
            {
                _logger.LogWarning("Thread {ThreadId} not found or does not belong to account {AccountId}", 
                    threadId, accountId);
            }

            return thread;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thread {ThreadId} for account {AccountId}", threadId, accountId);
            throw;
        }
    }

    /// <summary>
    /// Get all active threads for an account
    /// </summary>
    public async Task<IEnumerable<ConversationThread>> GetActiveThreadsForAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var threads = await _threadRepository.GetActiveThreadsAsync(accountId, 20, cancellationToken);

            _logger.LogDebug("Found {ThreadCount} active threads for account {AccountId}", threads.Count(), accountId);
            return threads;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active threads for account {AccountId}", accountId);
            throw;
        }
    }

    /// <summary>
    /// Create a new conversation thread
    /// </summary>
    public async Task<ConversationThread> CreateNewThreadAsync(int accountId, string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var newThread = new ConversationThread(accountId, title);
            
            var createdThread = await _threadRepository.AddAsync(newThread, cancellationToken);

            _logger.LogInformation("Created new conversation thread {ThreadId} for account {AccountId} with title '{Title}'", 
                createdThread.Id, accountId, title);

            return createdThread;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new thread for account {AccountId} with title '{Title}'", accountId, title);
            throw;
        }
    }

    /// <summary>
    /// Update a thread's title
    /// </summary>
    public async Task<ConversationThread> UpdateThreadTitleAsync(int threadId, int accountId, string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

            if (thread == null)
            {
                throw new InvalidOperationException($"Thread {threadId} not found or does not belong to account {accountId}");
            }

            thread.UpdateTitle(title);
            var updatedThread = await _threadRepository.UpdateAsync(thread, cancellationToken);

            _logger.LogInformation("Updated thread {ThreadId} title to '{Title}' for account {AccountId}", 
                threadId, title, accountId);

            return updatedThread;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating thread {ThreadId} title for account {AccountId}", threadId, accountId);
            throw;
        }
    }

    /// <summary>
    /// Deactivate a conversation thread
    /// </summary>
    public async Task DeactivateThreadAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

            if (thread == null)
            {
                throw new InvalidOperationException($"Thread {threadId} not found or does not belong to account {accountId}");
            }

            thread.Deactivate();
            await _threadRepository.UpdateAsync(thread, cancellationToken);

            _logger.LogInformation("Deactivated thread {ThreadId} for account {AccountId}", threadId, accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating thread {ThreadId} for account {AccountId}", threadId, accountId);
            throw;
        }
    }

    /// <summary>
    /// Create a daily summary for a conversation thread
    /// </summary>
    public async Task<MemorySummary> CreateDailySummaryAsync(int threadId, DateOnly summaryDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if summary already exists for this date
            var existingSummary = await _summaryRepository.GetByThreadAndDateAsync(threadId, summaryDate, cancellationToken);

            if (existingSummary != null)
            {
                _logger.LogDebug("Summary already exists for thread {ThreadId} on {Date}", threadId, summaryDate);
                return existingSummary;
            }

            // Get messages for the specified date
            var startDate = summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endDate = summaryDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var messages = await _messageRepository.GetMessagesByDateRangeAsync(threadId, startDate, endDate, cancellationToken);
            var messageList = messages.ToList();

            if (!messageList.Any())
            {
                _logger.LogDebug("No messages found for thread {ThreadId} on {Date}", threadId, summaryDate);
                throw new InvalidOperationException($"No messages found for thread {threadId} on {summaryDate}");
            }

            // Create a simple summary (this could be enhanced with AI summarization)
            var userMessages = messageList.Where(m => m.Role == "user").Count();
            var assistantMessages = messageList.Where(m => m.Role == "assistant").Count();
            var totalTokens = messageList.Sum(m => m.TokenCount);

            var summary = $"Conversation on {summaryDate:yyyy-MM-dd} with {userMessages} user messages and {assistantMessages} assistant responses. " +
                         $"Total tokens: {totalTokens}. " +
                         $"Topics discussed: {string.Join(", ", ExtractKeyTopics(messageList))}";

            var keyTopics = System.Text.Json.JsonSerializer.Serialize(ExtractKeyTopics(messageList));
            var userPreferences = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>());

            var memorySummary = new MemorySummary(
                threadId, 
                summaryDate, 
                summary, 
                keyTopics, 
                userPreferences, 
                messageList.Count, 
                totalTokens);

            var createdSummary = await _summaryRepository.AddAsync(memorySummary, cancellationToken);

            _logger.LogInformation("Created daily summary for thread {ThreadId} on {Date} with {MessageCount} messages", 
                threadId, summaryDate, messageList.Count);

            return createdSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating daily summary for thread {ThreadId} on {Date}", threadId, summaryDate);
            throw;
        }
    }

    /// <summary>
    /// Clean up old inactive threads
    /// </summary>
    public async Task CleanupOldThreadsAsync(int accountId, TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - retentionPeriod;

            var oldThreads = await _threadRepository.GetInactiveOldThreadsAsync(accountId, cutoffDate, cancellationToken);
            var threadList = oldThreads.ToList();

            if (threadList.Any())
            {
                await _threadRepository.DeleteRangeAsync(threadList, cancellationToken);

                _logger.LogInformation("Cleaned up {ThreadCount} old threads for account {AccountId}", 
                    threadList.Count, accountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old threads for account {AccountId}", accountId);
            throw;
        }
    }

    /// <summary>
    /// Extract key topics from conversation messages (simple keyword extraction)
    /// </summary>
    private static string[] ExtractKeyTopics(List<ChatMessage> messages)
    {
        var commonWords = new HashSet<string> { "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "a", "an" };
        var topics = new HashSet<string>();

        foreach (var message in messages.Where(m => m.Role == "user"))
        {
            var words = message.Content
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('.', '?', '!', ',', ';', ':').ToLowerInvariant())
                .Where(w => w.Length > 3 && !commonWords.Contains(w))
                .Take(5);

            foreach (var word in words)
            {
                topics.Add(word);
            }
        }

        return topics.Take(10).ToArray();
    }
}