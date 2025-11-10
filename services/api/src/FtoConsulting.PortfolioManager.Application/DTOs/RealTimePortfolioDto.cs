namespace FtoConsulting.PortfolioManager.Application.DTOs;

/// <summary>
/// Real-time portfolio data with current market values
/// </summary>
public record RealTimePortfolioDto(
    int AccountId,
    DateOnly LatestHoldingsDate,
    DateTime PriceTimestamp,
    decimal TotalValue,
    decimal TotalCost,
    decimal UnrealizedGainLoss,
    decimal UnrealizedGainLossPercentage,
    int TotalHoldings,
    int HoldingsWithRealTimePrices,
    IEnumerable<RealTimeHoldingDto> Holdings
);

/// <summary>
/// Individual holding with real-time market value
/// </summary>
public record RealTimeHoldingDto(
    int HoldingId,
    string Ticker,
    string InstrumentName,
    decimal Quantity,
    decimal CostBasis,
    decimal? RealTimePrice,
    bool HasRealTimePrice,
    decimal CurrentValue,
    decimal UnrealizedGainLoss,
    decimal UnrealizedGainLossPercentage,
    DateOnly HoldingDate,
    DateTime? PriceTimestamp
);