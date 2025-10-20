namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

/// <summary>
/// Portfolio analysis results DTO
/// </summary>
public record PortfolioAnalysisDto(
    int AccountId,
    DateTime AnalysisDate,
    decimal TotalValue,
    decimal DayChange,
    decimal DayChangePercentage,
    IEnumerable<HoldingPerformanceDto> HoldingPerformance,
    PerformanceMetricsDto Metrics
);

/// <summary>
/// Individual holding performance within portfolio analysis
/// </summary>
public record HoldingPerformanceDto(
    string Ticker,
    string InstrumentName,
    decimal UnitAmount,
    decimal CurrentValue,
    decimal BoughtValue,
    decimal DayChange,
    decimal DayChangePercentage,
    decimal TotalReturn,
    decimal TotalReturnPercentage,
    string PerformanceContext
);

/// <summary>
/// Performance comparison between two dates
/// </summary>
public record PerformanceComparisonDto(
    int AccountId,
    DateTime StartDate,
    DateTime EndDate,
    decimal StartValue,
    decimal EndValue,
    decimal TotalChange,
    decimal TotalChangePercentage,
    IEnumerable<HoldingComparisonDto> HoldingComparisons,
    ComparisonInsightsDto Insights
);

/// <summary>
/// Individual holding comparison between dates
/// </summary>
public record HoldingComparisonDto(
    string Ticker,
    string InstrumentName,
    decimal StartValue,
    decimal EndValue,
    decimal Change,
    decimal ChangePercentage,
    string PerformanceCategory
);

/// <summary>
/// Portfolio performance metrics
/// </summary>
public record PerformanceMetricsDto(
    decimal TotalReturn,
    decimal TotalReturnPercentage,
    decimal DailyVolatility,
    string RiskProfile,
    IEnumerable<string> TopPerformers,
    IEnumerable<string> BottomPerformers
);

/// <summary>
/// Insights from performance comparison
/// </summary>
public record ComparisonInsightsDto(
    string OverallTrend,
    IEnumerable<string> KeyDrivers,
    IEnumerable<string> RiskFactors,
    IEnumerable<string> Opportunities
);