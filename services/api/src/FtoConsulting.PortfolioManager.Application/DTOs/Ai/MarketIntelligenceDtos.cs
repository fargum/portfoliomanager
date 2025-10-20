namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

/// <summary>
/// Market context information for portfolio analysis
/// </summary>
public record MarketContextDto(
    IEnumerable<string> Tickers,
    DateTime Date,
    string MarketSummary,
    IEnumerable<NewsItemDto> RelevantNews,
    MarketSentimentDto Sentiment,
    IEnumerable<MarketIndexDto> Indices
);

/// <summary>
/// Financial news item
/// </summary>
public record NewsItemDto(
    string Title,
    string Summary,
    string Source,
    DateTime PublishedDate,
    string Url,
    IEnumerable<string> RelatedTickers,
    decimal SentimentScore,
    string Category
);

/// <summary>
/// Market sentiment data
/// </summary>
public record MarketSentimentDto(
    DateTime Date,
    decimal OverallSentimentScore,
    string SentimentLabel,
    decimal FearGreedIndex,
    IEnumerable<SectorSentimentDto> SectorSentiments,
    IEnumerable<MarketIndicatorDto> Indicators
);

/// <summary>
/// Sector-specific sentiment
/// </summary>
public record SectorSentimentDto(
    string SectorName,
    decimal SentimentScore,
    string Trend,
    IEnumerable<string> KeyFactors
);

/// <summary>
/// Market index information
/// </summary>
public record MarketIndexDto(
    string Name,
    string Symbol,
    decimal CurrentValue,
    decimal DayChange,
    decimal DayChangePercentage,
    DateTime LastUpdated
);

/// <summary>
/// Market indicator data
/// </summary>
public record MarketIndicatorDto(
    string Name,
    decimal Value,
    string Trend,
    string Description,
    DateTime Date
);