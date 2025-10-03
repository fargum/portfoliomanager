using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IHoldingRepository : IRepository<Holding>
{
    Task<IEnumerable<Holding>> GetByPortfolioIdAsync(Guid portfolioId);
    Task<IEnumerable<Holding>> GetByInstrumentIdAsync(Guid instrumentId);
    Task<IEnumerable<Holding>> GetByValuationDateAsync(DateTime valuationDate);
    Task<IEnumerable<Holding>> GetByPortfolioAndDateRangeAsync(Guid portfolioId, DateTime fromDate, DateTime toDate);
    Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(Guid accountId, DateTime valuationDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetDistinctIsinsByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default);
}