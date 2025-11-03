using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for managing instrument price data
/// </summary>
public interface IInstrumentPriceRepository : IRepository<InstrumentPrice>
{
    /// <summary>
    /// Gets price data for a specific instrument and valuation date
    /// </summary>
    /// <param name="instrumentId">ID of the instrument</param>
    /// <param name="valuationDate">Valuation date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instrument price data if found, null otherwise</returns>
    Task<InstrumentPrice?> GetByInstrumentAndDateAsync(int instrumentId, DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all price data for a specific valuation date
    /// </summary>
    /// <param name="valuationDate">Valuation date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of instrument prices</returns>
    Task<IEnumerable<InstrumentPrice>> GetByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all price data for a specific instrument across all dates
    /// </summary>
    /// <param name="instrumentId">ID of the instrument</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of instrument prices</returns>
    Task<IEnumerable<InstrumentPrice>> GetByInstrumentAsync(int instrumentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest price data for a specific instrument before or on a given date
    /// </summary>
    /// <param name="instrumentId">ID of the instrument</param>
    /// <param name="beforeOrOnDate">Date to search before or on</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest instrument price data if found, null otherwise</returns>
    Task<InstrumentPrice?> GetLatestPriceAsync(int instrumentId, DateOnly beforeOrOnDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all price data for a specific valuation date
    /// </summary>
    /// <param name="valuationDate">Valuation date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records deleted</returns>
    Task<int> DeleteByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts or updates price data for a valuation date
    /// </summary>
    /// <param name="priceData">Collection of price data to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BulkUpsertAsync(IEnumerable<InstrumentPrice> priceData, CancellationToken cancellationToken = default);
}