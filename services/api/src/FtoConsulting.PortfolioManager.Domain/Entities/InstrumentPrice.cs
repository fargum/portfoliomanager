using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Represents market price data for an instrument at a specific valuation date
/// </summary>
public class InstrumentPrice : BaseEntity
{
    /// <summary>
    /// ID of the instrument (part of composite primary key)
    /// </summary>
    public int InstrumentId { get; set; }

    /// <summary>
    /// Valuation date for the price data (part of composite primary key)
    /// </summary>
    public DateOnly ValuationDate { get; set; }

    /// <summary>
    /// Trading ticker for the instrument
    /// </summary>
    public string? Ticker { get; set; }

    /// <summary>
    /// Name of the instrument
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Current market price
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Currency of the price
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Price change from previous close
    /// </summary>
    public decimal? Change { get; set; }

    /// <summary>
    /// Percentage change from previous close
    /// </summary>
    public decimal? ChangePercent { get; set; }

    /// <summary>
    /// Previous closing price
    /// </summary>
    public decimal? PreviousClose { get; set; }

    /// <summary>
    /// Opening price for the trading day
    /// </summary>
    public decimal? Open { get; set; }

    /// <summary>
    /// Highest price during the trading day
    /// </summary>
    public decimal? High { get; set; }

    /// <summary>
    /// Lowest price during the trading day
    /// </summary>
    public decimal? Low { get; set; }

    /// <summary>
    /// Trading volume
    /// </summary>
    public long? Volume { get; set; }

    /// <summary>
    /// Market/exchange where the instrument is traded
    /// </summary>
    public string? Market { get; set; }

    /// <summary>
    /// Current market status (Open, Closed, etc.)
    /// </summary>
    public string? MarketStatus { get; set; }

    /// <summary>
    /// Timestamp when the price data was captured
    /// </summary>
    public DateTime? PriceTimestamp { get; set; }

    /// <summary>
    /// Navigation property to the related instrument
    /// </summary>
    public virtual Instrument? Instrument { get; set; }
}