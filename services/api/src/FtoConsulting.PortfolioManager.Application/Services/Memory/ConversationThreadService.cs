using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Memory;

/// <summary>
/// Service for managing conversation threads and memory operations with AI-powered summarization
/// </summary>
public class ConversationThreadService : IConversationThreadService
{
    private readonly IConversationThreadRepository _threadRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IMemorySummaryRepository _summaryRepository;
    private readonly IMemoryExtractionService _memoryExtractionService;
    private readonly ILogger<ConversationThreadService> _logger;

    public ConversationThreadService(
        IConversationThreadRepository threadRepository,
        IChatMessageRepository messageRepository,
        IMemorySummaryRepository summaryRepository,
        IMemoryExtractionService memoryExtractionService,
        ILogger<ConversationThreadService> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
        _memoryExtractionService = memoryExtractionService ?? throw new ArgumentNullException(nameof(memoryExtractionService));
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
                // Check if the thread should be closed due to inactivity (older than 30 minutes)
                var inactivityThreshold = TimeSpan.FromMinutes(30);
                if (DateTime.UtcNow - activeThread.LastActivity > inactivityThreshold)
                {
                    _logger.LogInformation("Closing inactive thread {ThreadId} for account {AccountId} (last activity: {LastActivity})", 
                        activeThread.Id, accountId, activeThread.LastActivity);
                    
                    // Create memory summary before closing - use thread object to avoid lookup
                    await CreateMemorySummaryForClosedThread(activeThread, cancellationToken);
                    
                    // Deactivate the old thread
                    await CloseThreadAsync(activeThread.Id, accountId, cancellationToken);
                    
                    // Create a new thread for this session
                    return await CreateNewThreadAsync(accountId, $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}", cancellationToken);
                }

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
    /// Create a new session-based conversation thread (when UI doesn't provide threadId)
    /// </summary>
    public async Task<ConversationThread> CreateNewSessionAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Close any existing active thread and create memory summary
            var existingActiveThread = await _threadRepository.GetMostRecentActiveThreadAsync(accountId, cancellationToken);
            if (existingActiveThread != null)
            {
                _logger.LogInformation("Starting new session for account {AccountId}, closing previous thread {ThreadId}", 
                    accountId, existingActiveThread.Id);
                
                // Create memory summary before closing
                await CreateMemorySummaryForClosedThread(existingActiveThread, cancellationToken);
                
                // Close the previous thread
                await CloseThreadAsync(existingActiveThread.Id, accountId, cancellationToken);
            }

            // Create a new session thread
            return await CreateNewThreadAsync(accountId, $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new session for account {AccountId}", accountId);
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
    /// Create a daily summary for conversation messages on a specific date using AI-powered memory extraction
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

            // Get the thread to determine account ID
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, 0, cancellationToken);
            if (thread == null)
            {
                throw new InvalidOperationException($"Thread {threadId} not found for daily summary creation");
            }

            // Use AI-powered memory extraction
            var extractionResult = await _memoryExtractionService.ExtractMemoriesFromConversationAsync(
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

            var createdSummary = await _summaryRepository.AddAsync(memorySummary, cancellationToken);

            _logger.LogInformation("Created AI-powered daily summary for thread {ThreadId} on {Date} with {MessageCount} messages and {FactCount} facts", 
                threadId, summaryDate, messageList.Count, extractionResult.ImportantFacts.Count);

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
    /// Close a conversation thread and mark it as inactive
    /// </summary>
    public async Task CloseThreadAsync(int threadId, int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, accountId, cancellationToken);
            if (thread == null)
            {
                _logger.LogWarning("Thread {ThreadId} not found for account {AccountId} when attempting to close", 
                    threadId, accountId);
                return;
            }

            // Deactivate the thread
            thread.Deactivate();
            await _threadRepository.UpdateAsync(thread, cancellationToken);

            _logger.LogInformation("Closed thread {ThreadId} for account {AccountId}", threadId, accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing thread {ThreadId} for account {AccountId}", threadId, accountId);
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
            _logger.LogInformation("MEMORY EXTRACTION: Starting CreateMemorySummaryForClosedThread for thread {ThreadId}", thread.Id);
            
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // Check if we already have a summary for today
            var existingSummary = await _summaryRepository.GetByThreadAndDateAsync(thread.Id, today, cancellationToken);
            if (existingSummary != null)
            {
                _logger.LogDebug("Memory summary already exists for thread {ThreadId} on {Date}", thread.Id, today);
                return;
            }

            // Get messages from the thread to summarize
            var messages = await _messageRepository.GetByThreadIdAsync(thread.Id, cancellationToken);
            var messageList = messages.ToList();

            _logger.LogInformation("MEMORY EXTRACTION: Found {MessageCount} messages for thread {ThreadId}", messageList.Count, thread.Id);

            if (!messageList.Any())
            {
                _logger.LogDebug("No messages found to summarize for thread {ThreadId}", thread.Id);
                return;
            }

            _logger.LogInformation("MEMORY EXTRACTION: Found thread {ThreadId} for account {AccountId}, starting AI extraction", thread.Id, thread.AccountId);

            // Use AI-powered memory extraction
            _logger.LogInformation("Starting AI-powered memory extraction for thread {ThreadId} with {MessageCount} messages", 
                thread.Id, messageList.Count);

            var extractionResult = await _memoryExtractionService.ExtractMemoriesFromConversationAsync(
                messageList.AsEnumerable(), 
                thread.AccountId, 
                cancellationToken);

            // Calculate total tokens (approximation: words * 1.3)
            var totalTokens = messageList.Sum(m => (m.Content?.Split(' ').Length ?? 0)) * 1.3;
            
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

            await _summaryRepository.AddAsync(memorySummary, cancellationToken);

            _logger.LogInformation("Created AI-powered memory summary for thread {ThreadId} with {FactCount} important facts and {PreferenceCount} preferences (confidence: {Confidence:P0})", 
                thread.Id, 
                extractionResult.ImportantFacts.Count, 
                extractionResult.UserPreferences.Count,
                extractionResult.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create memory summary for thread {ThreadId}", thread.Id);
            // Don't throw - summarization failure shouldn't break thread closure
        }
    }

    // Overload that accepts thread ID (for backward compatibility)
    private async Task CreateMemorySummaryForClosedThread(int threadId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("MEMORY EXTRACTION: Starting CreateMemorySummaryForClosedThread for thread {ThreadId}", threadId);
            
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // Check if we already have a summary for today
            var existingSummary = await _summaryRepository.GetByThreadAndDateAsync(threadId, today, cancellationToken);
            if (existingSummary != null)
            {
                _logger.LogDebug("Memory summary already exists for thread {ThreadId} on {Date}", threadId, today);
                return;
            }

            // Get messages from the thread to summarize
            var messages = await _messageRepository.GetByThreadIdAsync(threadId, cancellationToken);
            var messageList = messages.ToList();

            _logger.LogInformation("MEMORY EXTRACTION: Found {MessageCount} messages for thread {ThreadId}", messageList.Count, threadId);

            if (!messageList.Any())
            {
                _logger.LogDebug("No messages found to summarize for thread {ThreadId}", threadId);
                return;
            }

            // Get the thread to determine account ID
            var thread = await _threadRepository.GetByIdWithDetailsAsync(threadId, 0, cancellationToken);
            if (thread == null)
            {
                _logger.LogWarning("Thread {ThreadId} not found when creating memory summary", threadId);
                return;
            }

            _logger.LogInformation("MEMORY EXTRACTION: Found thread {ThreadId} for account {AccountId}, starting AI extraction", threadId, thread.AccountId);

            // Use AI-powered memory extraction
            _logger.LogInformation("Starting AI-powered memory extraction for thread {ThreadId} with {MessageCount} messages", 
                threadId, messageList.Count);

            var extractionResult = await _memoryExtractionService.ExtractMemoriesFromConversationAsync(
                messageList.AsEnumerable(), 
                thread.AccountId, 
                cancellationToken);

            _logger.LogInformation("MEMORY EXTRACTION: AI extraction completed for thread {ThreadId}. Facts: {FactCount}, Preferences: {PreferenceCount}, Confidence: {Confidence}", 
                threadId, extractionResult.ImportantFacts.Count, extractionResult.UserPreferences.Count, extractionResult.ConfidenceScore);

            // Create enhanced memory summary with AI-extracted data
            var totalTokens = messageList.Sum(m => m.TokenCount);
            var userMessages = messageList.Count(m => m.Role == "user");
            var assistantMessages = messageList.Count(m => m.Role == "assistant");

            var enhancedSummary = $"{extractionResult.Summary} " +
                                $"({userMessages} user messages, {assistantMessages} assistant responses, " +
                                $"confidence: {extractionResult.ConfidenceScore:P0})";

            var memorySummary = new MemorySummary(
                threadId, 
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
                totalTokens);

            await _summaryRepository.AddAsync(memorySummary, cancellationToken);

            _logger.LogInformation("Created AI-powered memory summary for thread {ThreadId} with {FactCount} important facts and {PreferenceCount} preferences (confidence: {Confidence:P0})", 
                threadId, 
                extractionResult.ImportantFacts.Count, 
                extractionResult.UserPreferences.Count,
                extractionResult.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create memory summary for thread {ThreadId}", threadId);
            // Don't throw - summarization failure shouldn't break thread closure
        }
    }

    /// <summary>
    /// Extract important facts from conversation (e.g., user name, preferences, goals)
    /// </summary>
    private static List<string> ExtractImportantFacts(List<ChatMessage> messages)
    {
        var facts = new List<string>();
        
        foreach (var message in messages.Where(m => m.Role == "user"))
        {
            var content = message.Content.ToLowerInvariant();
            
            // Extract name mentions
            if (content.Contains("my name is ") || content.Contains("i'm ") || content.Contains("i am "))
            {
                var namePatterns = new[]
                {
                    @"my name is (\w+)",
                    @"i'?m (\w+)",
                    @"i am (\w+)"
                };
                
                foreach (var pattern in namePatterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(content, pattern);
                    if (match.Success && match.Groups[1].Value.Length > 1)
                    {
                        facts.Add($"User name: {match.Groups[1].Value}");
                        break;
                    }
                }
            }
            
            // Extract portfolio goals
            if (content.Contains("goal") || content.Contains("objective") || content.Contains("want to"))
            {
                if (content.Length < 200) // Only short, clear statements
                {
                    facts.Add($"Goal: {content.Trim()}");
                }
            }
            
            // Extract risk tolerance mentions
            if (content.Contains("risk") && (content.Contains("toleranc") || content.Contains("averse") || content.Contains("aggressive")))
            {
                if (content.Length < 200)
                {
                    facts.Add($"Risk preference: {content.Trim()}");
                }
            }
        }
        
        return facts.Distinct().Take(10).ToList();
    }

    /// <summary>
    /// Extract user preferences from conversation
    /// </summary>
    private static Dictionary<string, string> ExtractUserPreferences(List<ChatMessage> messages)
    {
        var preferences = new Dictionary<string, string>();
        
        // This is a simple implementation - could be enhanced with NLP
        foreach (var message in messages.Where(m => m.Role == "user"))
        {
            var content = message.Content.ToLowerInvariant();
            
            if (content.Contains("prefer"))
            {
                preferences["communication_style"] = "prefers detailed explanations";
            }
            
            if (content.Contains("daily") || content.Contains("frequent"))
            {
                preferences["update_frequency"] = "daily updates preferred";
            }
        }
        
        return preferences;
    }

    /// <summary>
    /// Get memory summaries to include in new conversations for context continuity
    /// </summary>
    public async Task<List<string>> GetRelevantMemoriesAsync(int accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get recent memory summaries from closed threads for this account
            var recentSummaries = await _summaryRepository.GetRecentSummariesByAccountAsync(accountId, 5, cancellationToken);
            
            var memories = new List<string>();
            
            foreach (var summary in recentSummaries)
            {
                // Extract important facts from the summary
                if (!string.IsNullOrEmpty(summary.Summary))
                {
                    memories.Add(summary.Summary);
                }
            }
            
            _logger.LogDebug("Retrieved {MemoryCount} relevant memories for account {AccountId}", 
                memories.Count, accountId);
            
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve relevant memories for account {AccountId}", accountId);
            return new List<string>();
        }
    }
}