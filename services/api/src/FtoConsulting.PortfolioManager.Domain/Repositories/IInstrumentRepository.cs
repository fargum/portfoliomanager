using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IInstrumentRepository : IRepository<Instrument>
{
    Task<IEnumerable<Instrument>> GetByInstrumentTypeAsync(int instrumentTypeId);
    Task<IEnumerable<Instrument>> SearchByNameAsync(string name);
    Task<Instrument?> GetByNameAsync(string name);
    Task<Instrument?> GetByTickerAsync(string ticker);
}