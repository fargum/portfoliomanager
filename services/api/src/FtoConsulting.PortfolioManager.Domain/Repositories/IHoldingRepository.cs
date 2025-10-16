using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IHoldingRepository : IRepository<Holding>
{
    Task<IEnumerable<Holding>> GetByPortfolioIdAsync(int portfolioId);
    Task<IEnumerable<Holding>> GetByInstrumentIdAsync(int instrumentId);
    Task<IEnumerable<Holding>> GetByValuationDateAsync(DateTime valuationDate);
    Task<IEnumerable<Holding>> GetByPortfolioAndDateRangeAsync(int portfolioId, DateTime fromDate, DateTime toDate);
    Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateTime valuationDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetDistinctTickersByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<(string Ticker, string Name)>> GetDistinctInstrumentsByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<(string Ticker, string Name)>> GetAllDistinctInstrumentsAsync(CancellationToken cancellationToken = default);
    Task<DateOnly?> GetLatestValuationDateAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Holding>> GetHoldingsByValuationDateWithInstrumentsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);
    Task DeleteHoldingsByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default);
}