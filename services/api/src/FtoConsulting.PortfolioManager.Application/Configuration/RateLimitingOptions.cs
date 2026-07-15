namespace FtoConsulting.PortfolioManager.Application.Configuration;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Maximum AI chat requests per user per window.</summary>
    public int AiChatPermitLimit { get; init; } = 20;

    /// <summary>Sliding window duration in seconds for the ai-chat policy.</summary>
    public int AiChatWindowSeconds { get; init; } = 60;

    /// <summary>Maximum standard API requests per user per window.</summary>
    public int StandardApiPermitLimit { get; init; } = 100;

    /// <summary>Fixed window duration in seconds for the standard-api policy.</summary>
    public int StandardApiWindowSeconds { get; init; } = 60;
}
