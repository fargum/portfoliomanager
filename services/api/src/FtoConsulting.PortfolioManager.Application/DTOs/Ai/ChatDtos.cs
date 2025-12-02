namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

/// <summary>
/// Request DTO for chat-based portfolio queries
/// NOTE: AccountId is NOT in the request - it's retrieved from authenticated user context for security
/// </summary>
public record ChatRequestDto(
    string Query,
    DateTime? ContextDate = null,
    int? ThreadId = null,
    string? ThreadTitle = null,
    bool CreateNewThread = false
);

/// <summary>
/// Response DTO for chat-based portfolio queries
/// </summary>
public record ChatResponseDto(
    string Response,
    string QueryType,
    PortfolioSummaryDto? PortfolioSummary = null,
    IEnumerable<InsightDto>? Insights = null,
    int? ThreadId = null,
    string? ThreadTitle = null
)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// AI tool definition for MCP server
/// </summary>
public record AiToolDto(
    string Name,
    string Description,
    Dictionary<string, object> Parameters,
    string Category
);

/// <summary>
/// Portfolio summary for AI responses
/// </summary>
public record PortfolioSummaryDto(
    int AccountId,
    DateTime Date,
    decimal TotalValue,
    decimal DayChange,
    decimal DayChangePercentage,
    int HoldingsCount,
    IEnumerable<string> TopHoldings
);

/// <summary>
/// AI-generated insights
/// </summary>
public record InsightDto(
    string Type,
    string Title,
    string Description,
    string Severity,
    IEnumerable<string>? RelatedTickers = null
);

/// <summary>
/// Conversation thread DTO
/// </summary>
public record ConversationThreadDto(
    int Id,
    int AccountId,
    string ThreadTitle,
    DateTime LastActivity,
    bool IsActive,
    int MessageCount,
    DateTime CreatedAt
);

/// <summary>
/// Thread list response DTO
/// </summary>
public record ThreadListResponseDto(
    IEnumerable<ConversationThreadDto> Threads,
    int TotalThreads,
    int ActiveThreads
);

/// <summary>
/// Thread creation request DTO
/// </summary>
public record CreateThreadRequestDto(
    int AccountId,
    string ThreadTitle
);

/// <summary>
/// Thread update request DTO
/// </summary>
public record UpdateThreadRequestDto(
    int ThreadId,
    int AccountId,
    string? ThreadTitle = null,
    bool? IsActive = null
);