using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for managing exchange rate data
/// </summary>
public interface IExchangeRateRepository : IRepository<ExchangeRate>
{
    /// <summary>
    /// Gets the latest exchange rate for a currency pair on or before a specific date
    /// </summary>
    /// <param name="baseCurrency">Base currency (e.g., "USD")</param>
    /// <param name="targetCurrency">Target currency (e.g., "GBP")</param>
    /// <param name="onOrBeforeDate">Date to find rate for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate if found, null otherwise</returns>
    Task<ExchangeRate?> GetLatestRateAsync(string baseCurrency, string targetCurrency, DateOnly onOrBeforeDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all exchange rates for a specific date
    /// </summary>
    /// <param name="rateDate">Date to get rates for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of exchange rates</returns>
    Task<IEnumerable<ExchangeRate>> GetRatesByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts or updates exchange rates for a specific date
    /// </summary>
    /// <param name="rates">Collection of exchange rates to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BulkUpsertAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets exchange rate history for a currency pair
    /// </summary>
    /// <param name="baseCurrency">Base currency</param>
    /// <param name="targetCurrency">Target currency</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of historical exchange rates</returns>
    Task<IEnumerable<ExchangeRate>> GetRateHistoryAsync(string baseCurrency, string targetCurrency, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}