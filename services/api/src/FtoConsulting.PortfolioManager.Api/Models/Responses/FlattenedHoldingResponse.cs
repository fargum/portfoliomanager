using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Responses;

/// <summary>
/// Flattened holding data containing portfolio, instrument, and holding information
/// </summary>
public class FlattenedHoldingResponse
{
    /// <summary>
    /// Unique identifier for the holding
    /// </summary>
    /// <example>12345</example>
    public int HoldingId { get; set; }

    /// <summary>
    /// Date of the valuation
    /// </summary>
    /// <example>2025-09-27</example>
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// Number of units held
    /// </summary>
    /// <example>100.50</example>
    public decimal UnitAmount { get; set; }

    /// <summary>
    /// Original purchase value
    /// </summary>
    /// <example>1500.75</example>
    public decimal BoughtValue { get; set; }

    /// <summary>
    /// Current market value
    /// </summary>
    /// <example>1650.25</example>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Gain/loss amount (CurrentValue - BoughtValue)
    /// </summary>
    /// <example>149.50</example>
    public decimal GainLoss { get; set; }

    /// <summary>
    /// Gain/loss percentage
    /// </summary>
    /// <example>9.97</example>
    public decimal GainLossPercentage { get; set; }

    /// <summary>
    /// Daily profit or loss in absolute terms
    /// </summary>
    /// <example>25.50</example>
    public decimal? DailyProfitLoss { get; set; }

    /// <summary>
    /// Daily profit or loss as a percentage
    /// </summary>
    /// <example>1.55</example>
    public decimal? DailyProfitLossPercentage { get; set; }

    // Portfolio Information
    /// <summary>
    /// Unique identifier for the portfolio
    /// </summary>
    /// <example>a1b2c3d4-e5f6-7890-abcd-ef1234567890</example>
    public int PortfolioId { get; set; }

    /// <summary>
    /// Name of the portfolio
    /// </summary>
    /// <example>Growth Portfolio</example>
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>
    /// Account identifier that owns the portfolio
    /// </summary>
    /// <example>12345678-1234-5678-9012-123456789012</example>
    public int AccountId { get; set; }

    /// <summary>
    /// Name of the account that owns the portfolio
    /// </summary>
    /// <example>John Doe Investment Account</example>
    public string AccountName { get; set; } = string.Empty;

    // Instrument Information
    /// <summary>
    /// Unique identifier for the instrument
    /// </summary>
    /// <example>b2c3d4e5-f6g7-8901-bcde-f23456789012</example>
    public int InstrumentId { get; set; }



    /// <summary>
    /// Trading ticker symbol
    /// </summary>
    /// <example>AAPL</example>
    public string? Ticker { get; set; }

    /// <summary>
    /// Name of the instrument
    /// </summary>
    /// <example>Apple Inc. Common Stock</example>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the instrument
    /// </summary>
    /// <example>Common shares of Apple Inc., a technology company</example>
    public string? InstrumentDescription { get; set; }

    /// <summary>
    /// Type of the instrument
    /// </summary>
    /// <example>Equity</example>
    public string InstrumentType { get; set; } = string.Empty;

    // Platform Information
    /// <summary>
    /// Unique identifier for the platform where the holding is held
    /// </summary>
    /// <example>c3d4e5f6-g7h8-9012-cdef-345678901234</example>
    public int PlatformId { get; set; }

    /// <summary>
    /// Name of the platform
    /// </summary>
    /// <example>Interactive Brokers</example>
    public string PlatformName { get; set; } = string.Empty;

    // Real-time pricing information
    /// <summary>
    /// Current price per unit (only available for real-time data)
    /// </summary>
    /// <example>150.25</example>
    public decimal? CurrentPrice { get; set; }

    /// <summary>
    /// Indicates if the current price is real-time data
    /// </summary>
    /// <example>true</example>
    public bool IsRealTimePrice { get; set; }

    /// <summary>
    /// ISIN identifier for the instrument
    /// </summary>
    /// <example>US0378331005</example>
    public string? Isin { get; set; }

    /// <summary>
    /// SEDOL identifier for the instrument  
    /// </summary>
    /// <example>2046251</example>
    public string? Sedol { get; set; }
}