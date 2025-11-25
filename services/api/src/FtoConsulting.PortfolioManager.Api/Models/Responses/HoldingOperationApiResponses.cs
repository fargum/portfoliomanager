namespace FtoConsulting.PortfolioManager.Api.Models.Responses;

/// <summary>
/// API response model for holding operations
/// </summary>
public class HoldingOperationApiResponse
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
/// Information about an instrument
/// </summary>
public class InstrumentInfo
{
    /// <summary>
    /// Instrument ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Instrument ticker symbol
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Instrument name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Instrument description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string? CurrencyCode { get; set; }
    
    /// <summary>
    /// Quote unit (e.g., 'p' for pence, '$' for dollars)
    /// </summary>
    public string? QuoteUnit { get; set; }
    
    /// <summary>
    /// Instrument type ID
    /// </summary>
    public int InstrumentTypeId { get; set; }
}

/// <summary>
/// API response model for adding a new holding
/// </summary>
public class AddHoldingApiResponse : HoldingOperationApiResponse
{
    /// <summary>
    /// ID of the newly created holding
    /// </summary>
    public int? HoldingId { get; set; }

    /// <summary>
    /// Whether a new instrument was created
    /// </summary>
    public bool InstrumentCreated { get; set; }

    /// <summary>
    /// The instrument that was used/created
    /// </summary>
    public InstrumentInfo? Instrument { get; set; }

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
/// API response model for updating holding units
/// </summary>
public class UpdateHoldingApiResponse : HoldingOperationApiResponse
{
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

    /// <summary>
    /// ID of the updated holding
    /// </summary>
    public int HoldingId { get; set; }

    /// <summary>
    /// Ticker of the updated instrument
    /// </summary>
    public string? Ticker { get; set; }
}

/// <summary>
/// API response model for deleting a holding
/// </summary>
public class DeleteHoldingApiResponse : HoldingOperationApiResponse
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
/// Response for instrument check operations
/// </summary>
public class InstrumentCheckApiResponse
{
    /// <summary>
    /// The ticker that was checked
    /// </summary>
    public required string Ticker { get; set; }
    
    /// <summary>
    /// Whether the instrument exists in the system
    /// </summary>
    public bool Exists { get; set; }
    
    /// <summary>
    /// Instrument details if it exists
    /// </summary>
    public InstrumentInfo? Instrument { get; set; }
}