using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Requests;

/// <summary>
/// API request model for adding a new holding to a portfolio
/// </summary>
public class AddHoldingApiRequest
{
    /// <summary>
    /// Platform ID where the holding is held
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Platform ID must be greater than 0")]
    public int PlatformId { get; set; }

    /// <summary>
    /// Instrument ticker symbol
    /// </summary>
    [Required]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Ticker must be between 1 and 20 characters")]
    public required string Ticker { get; set; }

    /// <summary>
    /// Number of units
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Units must be greater than 0")]
    public decimal Units { get; set; }

    /// <summary>
    /// Original purchase value
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Bought value must be non-negative")]
    public decimal BoughtValue { get; set; }

    /// <summary>
    /// Instrument name (optional - will use ticker if not provided)
    /// </summary>
    [StringLength(200, ErrorMessage = "Instrument name cannot exceed 200 characters")]
    public string? InstrumentName { get; set; }

    /// <summary>
    /// Instrument description (optional)
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Instrument type ID (optional, defaults to equity)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Instrument type ID must be greater than 0")]
    public int? InstrumentTypeId { get; set; }

    /// <summary>
    /// Currency code (optional, defaults to GBP)
    /// </summary>
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be exactly 3 characters")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency code must be 3 uppercase letters")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Quote unit (optional, defaults to currency)
    /// </summary>
    [StringLength(10, ErrorMessage = "Quote unit cannot exceed 10 characters")]
    public string? QuoteUnit { get; set; }
}