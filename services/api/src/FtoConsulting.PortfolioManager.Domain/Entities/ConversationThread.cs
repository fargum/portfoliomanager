using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Represents a conversation thread for AI interactions, scoped to a specific account
/// </summary>
public class ConversationThread : BaseEntity
{
    public int AccountId { get; private set; }
    public string ThreadTitle { get; private set; } = string.Empty;
    public DateTime LastActivity { get; private set; }
    public bool IsActive { get; private set; }
    
    // Navigation properties
    public virtual Account Account { get; private set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; private set; } = [];
    public virtual ICollection<MemorySummary> Summaries { get; private set; } = [];

    // Private constructor for EF Core
    private ConversationThread() { }

    public ConversationThread(int accountId, string threadTitle)
    {
        AccountId = accountId;
        ThreadTitle = threadTitle ?? throw new ArgumentNullException(nameof(threadTitle));
        LastActivity = DateTime.UtcNow;
        IsActive = true;
    }

    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void UpdateTitle(string title)
    {
        ThreadTitle = title ?? throw new ArgumentNullException(nameof(title));
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }

    public void Reactivate()
    {
        IsActive = true;
        LastActivity = DateTime.UtcNow;
        SetUpdatedAt();
    }
}