using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.DTOs;

/// <summary>
/// Request model for adding a new holding to a portfolio
/// </summary>
public class AddHoldingRequest
{
    /// <summary>
    /// Platform ID where the holding is held
    /// </summary>
    public int PlatformId { get; set; }

    /// <summary>
    /// Instrument ticker symbol
    /// </summary>
    public required string Ticker { get; set; }

    /// <summary>
    /// Number of units
    /// </summary>
    public decimal Units { get; set; }

    /// <summary>
    /// Original purchase value
    /// </summary>
    public decimal BoughtValue { get; set; }

    /// <summary>
    /// Instrument name (required if instrument doesn't exist)
    /// </summary>
    public string? InstrumentName { get; set; }

    /// <summary>
    /// Instrument description (optional)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Instrument type ID (optional, defaults to equity)
    /// </summary>
    public int? InstrumentTypeId { get; set; }

    /// <summary>
    /// Currency code (optional)
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Quote unit (optional)
    /// </summary>
    public string? QuoteUnit { get; set; }
}

/// <summary>
/// Base result for holding operations
/// </summary>
public abstract class HoldingOperationResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// List of validation or processing errors
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of updating a holding's units
/// </summary>
public class HoldingUpdateResult : HoldingOperationResult
{
    /// <summary>
    /// The updated holding (null if operation failed)
    /// </summary>
    public Holding? UpdatedHolding { get; set; }

    /// <summary>
    /// Previous unit amount
    /// </summary>
    public decimal PreviousUnits { get; set; }

    /// <summary>
    /// New unit amount
    /// </summary>
    public decimal NewUnits { get; set; }

    /// <summary>
    /// Previous current value
    /// </summary>
    public decimal PreviousCurrentValue { get; set; }

    /// <summary>
    /// New current value after recalculation
    /// </summary>
    public decimal NewCurrentValue { get; set; }
}

/// <summary>
/// Result of adding a new holding
/// </summary>
public class HoldingAddResult : HoldingOperationResult
{
    /// <summary>
    /// The newly created holding (null if operation failed)
    /// </summary>
    public Holding? CreatedHolding { get; set; }

    /// <summary>
    /// Whether a new instrument was created
    /// </summary>
    public bool InstrumentCreated { get; set; }

    /// <summary>
    /// The instrument that was used/created
    /// </summary>
    public Instrument? Instrument { get; set; }

    /// <summary>
    /// Current market price used for valuation
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Calculated current value
    /// </summary>
    public decimal CurrentValue { get; set; }
}

/// <summary>
/// Result of deleting a holding
/// </summary>
public class HoldingDeleteResult : HoldingOperationResult
{
    /// <summary>
    /// ID of the holding that was deleted
    /// </summary>
    public int DeletedHoldingId { get; set; }

    /// <summary>
    /// Ticker of the deleted holding's instrument
    /// </summary>
    public string? DeletedTicker { get; set; }

    /// <summary>
    /// Portfolio ID where the holding was deleted from
    /// </summary>
    public int PortfolioId { get; set; }
}

/// <summary>
/// Result of pricing calculation for a holding
/// </summary>
public class HoldingPriceResult
{
    /// <summary>
    /// Current market price per unit
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Calculated current value (units × price with currency conversion)
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Whether pricing was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if pricing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates if this is a CASH instrument (no pricing needed)
    /// </summary>
    public bool IsCashInstrument { get; set; }
}