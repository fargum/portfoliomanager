namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

/// <summary>
/// Request DTO for chat-based portfolio queries
/// </summary>
public record ChatRequestDto(
    string Query,
    int AccountId,
    DateTime? ContextDate = null
);

/// <summary>
/// Response DTO for chat-based portfolio queries
/// </summary>
public record ChatResponseDto(
    string Response,
    string QueryType,
    PortfolioSummaryDto? PortfolioSummary = null,
    IEnumerable<InsightDto>? Insights = null
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