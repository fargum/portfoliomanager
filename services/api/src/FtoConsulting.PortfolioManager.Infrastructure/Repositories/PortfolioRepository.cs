using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public class PortfolioRepository : Repository<Portfolio>, IPortfolioRepository
{
    public PortfolioRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Portfolio>> GetByAccountIdAsync(int accountId)
    {
        return await _dbSet
            .Where(p => p.AccountId == accountId)
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Instrument)
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Platform)
            .ToListAsync();
    }

    public async Task<Portfolio?> GetByAccountAndNameAsync(int accountId, string name)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.AccountId == accountId && p.Name == name);
    }

    public async Task<Portfolio?> GetWithHoldingsAsync(int portfolioId)
    {
        return await _dbSet
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Instrument)
                    .ThenInclude(i => i.InstrumentType)
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Platform)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == portfolioId);
    }
}