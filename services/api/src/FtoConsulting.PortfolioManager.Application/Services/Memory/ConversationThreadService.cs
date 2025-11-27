using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Memory;

/// <summary>
/// Service for managing conversation threads and memory operations with AI-powered summarization
/// </summary>
public class ConversationThreadService(
    IConversationThreadRepository threadRepository,
    IChatMessageRepository messageRepository,
    IMemorySummaryRepository summaryRepository,
    IMemoryExtractionService memoryExtractionService,
    ILogger<ConversationThreadService> logger) : IConversationThreadService
{
    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromMinutes(30);
    private const int MaxActiveThreadsToRetrieve = 20;
    private const int MaxRecentSummaries = 5;
    private const double TokenEstimateMultiplier = 1.3;

    /// <summary>
    /// Get or create an active conversation thread for the account
    /// </summary>
    public async Task<ConversationThread> GetOrCreateActiveThreadAsync(int accountId, CancellationToken cancellationToken = default)
    {
        // Look for the most recent active thread
        var activeThread = await threadRepository.GetMostRecentActiveThreadAsync(accountId, cancellationToken);

            if (activeThread != null)
            {
                // Check if the thread should be closed due to inactivity
                if (DateTime.UtcNow - activeThread.LastActivity > InactivityThreshold)
                {
                    logger.LogInformation("Closing inactive thread {ThreadId} for account {AccountId} (last activity: {LastActivity})", 
                        activeThread.Id, accountId, activeThread.LastActivity);
                    
                    // Create memory summary before closing - use thread object to avoid lookup
                    await CreateMemorySummaryForClosedThread(activeThread, cancellationToken);
                    
                    // Deactivate the old thread
                    await CloseThreadAsync(activeThread.Id, accountId, cancellationToken);
                    
                    // Create a new thread for this session
                    return await CreateNewThreadAsync(accountId, $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}", cancellationToken);
                }

                logger.LogDebug("Found existing active thread {ThreadId} for account {AccountId}", 
                    activeThread.Id, accountId);
                return activeThread;
            }

        // Create a new thread if none exists
        return await CreateNewThreadAsync(accountId, $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}", cancellationToken);
    }

    /// <summary>
    /// Create a new session-based conversation thread (when UI doesn't provide threadId)
    /// </summary>
    public async Task<ConversationThread> CreateNewSessionAsync(int accountId, CancellationToken cancellationToken = default)
    {
        // Close any existing active thread and create memory summary
        var existingActiveThread = await threadRepository.GetMostRecentActiveThreadAsync(accountId, cancellationToken);
            if (existingActiveThread != null)
            {
                logger.LogInformation("Starting new session for account {AccountId}, closing previous thread {ThreadId}", 
                    accountId, existingActiveThread.Id);
                
                // Create memory summary before closing
                await CreateMemorySummaryForClosedThread(existingActiveThread, cancellationToken);
                
                // Close the previous thread
                await CloseThreadAsync(existingActiveThread.Id, accountId, cancellationToken);
            }

        // Create a new session thread
        return await CreateNewThreadAsync(accountId, $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}", cancellationToken);
    }

    /// <summary>
    /// Get a specific thread by ID, ensuring it belongs to the account
    /// </summary>
    public async Task<ConversationThread?> GetThreadByIdAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        var thread = await threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

        if (thread == null)
        {
            logger.LogWarning("Thread {ThreadId} not found or does not belong to account {AccountId}", 
                threadId, accountId);
        }

        return thread;
    }

    /// <summary>
    /// Get all active threads for an account
    /// </summary>
    public async Task<IEnumerable<ConversationThread>> GetActiveThreadsForAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var threads = await threadRepository.GetActiveThreadsAsync(accountId, MaxActiveThreadsToRetrieve, cancellationToken);

        logger.LogDebug("Found {ThreadCount} active threads for account {AccountId}", threads.Count(), accountId);
        return threads;
    }

    /// <summary>
    /// Create a new conversation thread
    /// </summary>
    public async Task<ConversationThread> CreateNewThreadAsync(int accountId, string title, CancellationToken cancellationToken = default)
    {
        var newThread = new ConversationThread(accountId, title);
        
        var createdThread = await threadRepository.AddAsync(newThread, cancellationToken);

        logger.LogInformation("Created new conversation thread {ThreadId} for account {AccountId} with title '{Title}'", 
            createdThread.Id, accountId, title);

        return createdThread;
    }

    /// <summary>
    /// Update a thread's title
    /// </summary>
    public async Task<ConversationThread> UpdateThreadTitleAsync(int threadId, int accountId, string title, CancellationToken cancellationToken = default)
    {
        var thread = await threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

        if (thread == null)
        {
            throw new InvalidOperationException($"Thread {threadId} not found or does not belong to account {accountId}");
        }

        thread.UpdateTitle(title);
        var updatedThread = await threadRepository.UpdateAsync(thread, cancellationToken);

        logger.LogInformation("Updated thread {ThreadId} title to '{Title}' for account {AccountId}", 
            threadId, title, accountId);

        return updatedThread;
    }

    /// <summary>
    /// Deactivate a conversation thread
    /// </summary>
    public async Task DeactivateThreadAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);

            if (thread == null)
            {
                throw new InvalidOperationException($"Thread {threadId} not found or does not belong to account {accountId}");
            }

            thread.Deactivate();
            await threadRepository.UpdateAsync(thread, cancellationToken);

            logger.LogInformation("Deactivated thread {ThreadId} for account {AccountId}", threadId, accountId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deactivating thread {ThreadId} for account {AccountId}", threadId, accountId);
            throw;
        }
    }

    /// <summary>
    /// Create a daily summary for conversation messages on a specific date using AI-powered memory extraction
    /// </summary>
    public async Task<MemorySummary> CreateDailySummaryAsync(int threadId, DateOnly summaryDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if summary already exists for this date
            var existingSummary = await summaryRepository.GetByThreadAndDateAsync(threadId, summaryDate, cancellationToken);

            
            if (existingSummary != null)
            {
                logger.LogDebug("Summary already exists for thread {ThreadId} on {Date}", threadId, summaryDate);
                return existingSummary;
            }

            // Get messages for the specified date
            var startDate = summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endDate = summaryDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var messages = await messageRepository.GetMessagesByDateRangeAsync(threadId, startDate, endDate, cancellationToken);
            var messageList = messages.ToList();

            if (!messageList.Any())
            {
                logger.LogDebug("No messages found for thread {ThreadId} on {Date}", threadId, summaryDate);
                throw new InvalidOperationException($"No messages found for thread {threadId} on {summaryDate}");
            }

            // Get the thread to determine account ID
            var thread = await threadRepository.GetByIdWithDetailsAsync(threadId, 0, cancellationToken);
            if (thread == null)
            {
                throw new InvalidOperationException($"Thread {threadId} not found for daily summary creation");
            }

            // Use AI-powered memory extraction
            var extractionResult = await memoryExtractionService.ExtractMemoriesFromConversationAsync(
                messageList, 
                thread.AccountId, 
                cancellationToken);

            var userMessages = messageList.Count(m => m.Role == "user");
            var assistantMessages = messageList.Count(m => m.Role == "assistant");
            var totalTokens = messageList.Sum(m => m.TokenCount);

            var enhancedSummary = $"Daily Summary ({summaryDate:yyyy-MM-dd}): {extractionResult.Summary} " +
                                $"({userMessages} user messages, {assistantMessages} assistant responses, " +
                                $"confidence: {extractionResult.ConfidenceScore:P0})";

            var memorySummary = new MemorySummary(
                threadId, 
                summaryDate, 
                enhancedSummary, 
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    keyTopics = extractionResult.KeyTopics,
                    importantFacts = extractionResult.ImportantFacts,
                    confidenceScore = extractionResult.ConfidenceScore
                }), 
                System.Text.Json.JsonSerializer.Serialize(extractionResult.UserPreferences), 
                messageList.Count, 
                totalTokens);

            var createdSummary = await summaryRepository.AddAsync(memorySummary, cancellationToken);

            logger.LogInformation("Created AI-powered daily summary for thread {ThreadId} on {Date} with {MessageCount} messages and {FactCount} facts", 
                threadId, summaryDate, messageList.Count, extractionResult.ImportantFacts.Count);

            return createdSummary;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating daily summary for thread {ThreadId} on {Date}", threadId, summaryDate);
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

            var oldThreads = await threadRepository.GetInactiveOldThreadsAsync(accountId, cutoffDate, cancellationToken);
            var threadList = oldThreads.ToList();

            if (threadList.Any())
            {
                await threadRepository.DeleteRangeAsync(threadList, cancellationToken);

                logger.LogInformation("Cleaned up {ThreadCount} old threads for account {AccountId}", 
                    threadList.Count, accountId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up old threads for account {AccountId}", accountId);
            throw;
        }
    }

    /// <summary>
    /// Close a conversation thread and mark it as inactive
    /// </summary>
    public async Task CloseThreadAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);
            if (thread == null)
            {
                logger.LogWarning("Thread {ThreadId} not found for account {AccountId} when attempting to close", 
                    threadId, accountId);
                return;
            }

            // Deactivate the thread
            thread.Deactivate();
            await threadRepository.UpdateAsync(thread, cancellationToken);

            logger.LogInformation("Closed thread {ThreadId} for account {AccountId}", threadId, accountId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing thread {ThreadId} for account {AccountId}", threadId, accountId);
            throw;
        }
    }

    /// <summary>
    /// Create a memory summary for a thread that's being closed using AI-powered extraction
    /// </summary>
    // Overload that accepts thread object directly (when we already have it)
    private async Task CreateMemorySummaryForClosedThread(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // Check if we already have a summary for today
            var existingSummary = await summaryRepository.GetByThreadAndDateAsync(thread.Id, today, cancellationToken);
            if (existingSummary != null)
            {
                logger.LogDebug("Memory summary already exists for thread {ThreadId} on {Date}", thread.Id, today);
                return;
            }

            // Get messages from the thread to summarize
            var messages = await messageRepository.GetByThreadIdAsync(thread.Id, cancellationToken);
            var messageList = messages.ToList();

            if (!messageList.Any())
            {
                logger.LogDebug("No messages found to summarize for thread {ThreadId}", thread.Id);
                return;
            }

            // Use AI-powered memory extraction
            var extractionResult = await memoryExtractionService.ExtractMemoriesFromConversationAsync(
                messageList.AsEnumerable(), 
                thread.AccountId, 
                cancellationToken);

            // Calculate total tokens (approximation: words * 1.3)
            var totalTokens = messageList.Sum(m => (m.Content?.Split(' ').Length ?? 0)) * TokenEstimateMultiplier;
            
            // Create enhanced summary combining AI analysis with basic stats
            var enhancedSummary = $"{extractionResult.Summary} " +
                                  $"[Conversation: {messageList.Count} messages, " +
                                  $"{extractionResult.ImportantFacts.Count} key facts, " +
                                  $"{extractionResult.UserPreferences.Count} preferences noted]";

            var memorySummary = new MemorySummary(
                thread.Id, 
                today, 
                enhancedSummary, 
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    keyTopics = extractionResult.KeyTopics,
                    importantFacts = extractionResult.ImportantFacts,
                    confidenceScore = extractionResult.ConfidenceScore
                }), 
                System.Text.Json.JsonSerializer.Serialize(extractionResult.UserPreferences), 
                messageList.Count, 
                (int)totalTokens);

            await summaryRepository.AddAsync(memorySummary, cancellationToken);

            logger.LogInformation("Created AI-powered memory summary for thread {ThreadId} with {FactCount} important facts and {PreferenceCount} preferences (confidence: {Confidence:P0})", 
                thread.Id, 
                extractionResult.ImportantFacts.Count, 
                extractionResult.UserPreferences.Count,
                extractionResult.ConfidenceScore);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create memory summary for thread {ThreadId}", thread.Id);
            // Don't throw - summarization failure shouldn't break thread closure
        }
    }

    /// <summary>
    /// Get memory summaries to include in new conversations for context continuity
    /// </summary>
    public async Task<List<string>> GetRelevantMemoriesAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get recent memory summaries from closed threads for this account
            var recentSummaries = await summaryRepository.GetRecentSummariesByAccountAsync(accountId, MaxRecentSummaries, cancellationToken);
            
            var memories = new List<string>();
            
            foreach (var summary in recentSummaries)
            {
                // Extract important facts from the summary
                if (!string.IsNullOrEmpty(summary.Summary))
                {
                    memories.Add(summary.Summary);
                }
            }
            
            logger.LogDebug("Retrieved {MemoryCount} relevant memories for account {AccountId}", 
                memories.Count, accountId);
            
            return memories;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve relevant memories for account {AccountId}", accountId);
            return new List<string>();
        }
    }
}
