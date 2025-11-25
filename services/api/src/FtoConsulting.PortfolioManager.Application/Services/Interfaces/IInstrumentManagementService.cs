using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

/// <summary>
/// Service for managing instrument creation and validation
/// </summary>
public interface IInstrumentManagementService
{
    /// <summary>
    /// Ensures an instrument exists in the database, creating it if necessary
    /// </summary>
    /// <param name="instrumentData">Instrument data including ticker and details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The existing or newly created instrument</returns>
    Task<Instrument> EnsureInstrumentExistsAsync(Instrument instrumentData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing instrument by ticker or creates a new one with provided details
    /// </summary>
    /// <param name="ticker">Instrument ticker</param>
    /// <param name="name">Instrument name</param>
    /// <param name="description">Instrument description (optional)</param>
    /// <param name="instrumentTypeId">Instrument type ID</param>
    /// <param name="currencyCode">Currency code (optional)</param>
    /// <param name="quoteUnit">Quote unit (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The existing or newly created instrument</returns>
    Task<Instrument> GetOrCreateInstrumentAsync(
        string ticker,
        string name,
        string? description = null,
        int? instrumentTypeId = null,
        string? currencyCode = null,
        string? quoteUnit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if an instrument should be updated based on incoming data
    /// </summary>
    /// <param name="existing">Existing instrument</param>
    /// <param name="incoming">Incoming instrument data</param>
    /// <returns>True if the instrument should be updated</returns>
    bool ShouldUpdateInstrument(Instrument existing, Instrument incoming);
}