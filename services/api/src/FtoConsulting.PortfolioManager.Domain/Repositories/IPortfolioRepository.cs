using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<IEnumerable<Portfolio>> GetByAccountIdAsync(int accountId);
    Task<Portfolio?> GetByAccountAndNameAsync(int accountId, string name);
    Task<Portfolio?> GetWithHoldingsAsync(int portfolioId);
}