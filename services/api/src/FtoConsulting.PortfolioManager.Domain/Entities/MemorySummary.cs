using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Represents a daily summary of conversation threads for memory management
/// </summary>
public class MemorySummary : BaseEntity
{
    public int ConversationThreadId { get; private set; }
    public DateOnly SummaryDate { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string KeyTopics { get; private set; } = string.Empty; // JSON array of topics discussed
    public string UserPreferences { get; private set; } = string.Empty; // JSON object of learned preferences
    public int MessageCount { get; private set; }
    public int TotalTokens { get; private set; }

    // Navigation properties
    public virtual ConversationThread ConversationThread { get; private set; } = null!;

    // Private constructor for EF Core
    private MemorySummary() { }

    public MemorySummary(int conversationThreadId, DateOnly summaryDate, string summary, 
        string keyTopics, string userPreferences, int messageCount, int totalTokens)
    {
        ConversationThreadId = conversationThreadId;
        SummaryDate = summaryDate;
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        KeyTopics = keyTopics ?? throw new ArgumentNullException(nameof(keyTopics));
        UserPreferences = userPreferences ?? throw new ArgumentNullException(nameof(userPreferences));
        MessageCount = messageCount;
        TotalTokens = totalTokens;
    }

    public void UpdateSummary(string summary)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        SetUpdatedAt();
    }

    public void UpdateKeyTopics(string keyTopics)
    {
        KeyTopics = keyTopics ?? throw new ArgumentNullException(nameof(keyTopics));
        SetUpdatedAt();
    }

    public void UpdateUserPreferences(string userPreferences)
    {
        UserPreferences = userPreferences ?? throw new ArgumentNullException(nameof(userPreferences));
        SetUpdatedAt();
    }

    public void UpdateMetrics(int messageCount, int totalTokens)
    {
        MessageCount = messageCount;
        TotalTokens = totalTokens;
        SetUpdatedAt();
    }
}