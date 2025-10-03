using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<IEnumerable<Portfolio>> GetByAccountIdAsync(Guid accountId);
    Task<Portfolio?> GetByAccountAndNameAsync(Guid accountId, string name);
    Task<Portfolio?> GetWithHoldingsAsync(Guid portfolioId);
}