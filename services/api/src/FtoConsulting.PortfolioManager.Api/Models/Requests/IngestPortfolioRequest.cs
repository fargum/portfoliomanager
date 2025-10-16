using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Requests;

/// <summary>
/// Request model for ingesting portfolio holdings data
/// </summary>
public class IngestPortfolioRequest
{
    /// <summary>
    /// Name of the portfolio
    /// </summary>
    [Required]
    [StringLength(200)]
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>
    /// ID of the account that owns this portfolio
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// Collection of holdings to ingest
    /// </summary>
    [Required]
    public List<HoldingDto> Holdings { get; set; } = new();
}

/// <summary>
/// Holding data transfer object
/// </summary>
public class HoldingDto
{
    /// <summary>
    /// Date of the holding valuation
    /// </summary>
    [Required]
    public DateTime ValuationDate { get; set; }

    /// <summary>
    /// Platform/broker where the holding is held
    /// </summary>
    [Required]
    public int PlatformId { get; set; }

    /// <summary>
    /// Number of units held
    /// </summary>
    [Required]
    [Range(0.00000001, double.MaxValue, ErrorMessage = "Unit amount must be greater than zero")]
    public decimal UnitAmount { get; set; }

    /// <summary>
    /// Original purchase value/book value
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Bought value cannot be negative")]
    public decimal BoughtValue { get; set; }

    /// <summary>
    /// Current market value
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Current value cannot be negative")]
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Daily profit/loss amount (optional)
    /// </summary>
    public decimal? DailyProfitLoss { get; set; }

    /// <summary>
    /// Daily profit/loss percentage (optional)
    /// </summary>
    public decimal? DailyProfitLossPercentage { get; set; }

    /// <summary>
    /// Instrument details for this holding
    /// </summary>
    [Required]
    public InstrumentDto Instrument { get; set; } = new();
}

/// <summary>
/// Instrument data transfer object
/// </summary>
public class InstrumentDto
{
    /// <summary>
    /// Display name of the instrument
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the instrument
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Trading ticker symbol - primary identifier for the instrument
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// ISO 4217 currency code (3 characters)
    /// </summary>
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be exactly 3 characters")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// ISO quote unit code (3 characters)
    /// </summary>
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Quote unit must be exactly 3 characters")]
    public string? QuoteUnit { get; set; }

    /// <summary>
    /// Type/category of the instrument (equity, bond, etc.)
    /// </summary>
    [Required]
    public int InstrumentTypeId { get; set; }
}