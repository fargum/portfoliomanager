using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Represents an individual chat message within a conversation thread
/// </summary>
public class ChatMessage : BaseEntity
{
    public int ConversationThreadId { get; private set; }
    public string Role { get; private set; } = string.Empty; // "user" or "assistant"
    public string Content { get; private set; } = string.Empty;
    public string? Metadata { get; private set; } // JSON for storing additional data like token counts, function calls, etc.
    public int TokenCount { get; private set; }
    public DateTime MessageTimestamp { get; private set; }

    // Navigation properties
    public virtual ConversationThread ConversationThread { get; private set; } = null!;

    // Private constructor for EF Core
    private ChatMessage() { }

    public ChatMessage(int conversationThreadId, string role, string content, int tokenCount = 0, string? metadata = null)
    {
        ConversationThreadId = conversationThreadId;
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        TokenCount = tokenCount;
        Metadata = metadata;
        MessageTimestamp = DateTime.UtcNow;
    }

    public void UpdateContent(string content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        SetUpdatedAt();
    }

    public void UpdateMetadata(string? metadata)
    {
        Metadata = metadata;
        SetUpdatedAt();
    }

    public void UpdateTokenCount(int tokenCount)
    {
        TokenCount = tokenCount;
        SetUpdatedAt();
    }
}