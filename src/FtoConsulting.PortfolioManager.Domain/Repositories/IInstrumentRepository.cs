using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IInstrumentRepository : IRepository<Instrument>
{
    Task<Instrument?> GetByISINAsync(string isin);
    Task<Instrument?> GetBySEDOLAsync(string sedol);
    Task<IEnumerable<Instrument>> GetByInstrumentTypeAsync(Guid instrumentTypeId);
    Task<IEnumerable<Instrument>> SearchByNameAsync(string name);
}