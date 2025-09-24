using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public class HoldingRepository : Repository<Holding>, IHoldingRepository
{
    public HoldingRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Holding>> GetByPortfolioIdAsync(Guid portfolioId)
    {
        return await _dbSet
            .Where(h => h.PortfolioId == portfolioId)
            .Include(h => h.Instrument)
                .ThenInclude(i => i.InstrumentType)
            .Include(h => h.Platform)
            .OrderByDescending(h => h.ValuationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Holding>> GetByInstrumentIdAsync(Guid instrumentId)
    {
        return await _dbSet
            .Where(h => h.InstrumentId == instrumentId)
            .Include(h => h.Portfolio)
            .Include(h => h.Platform)
            .OrderByDescending(h => h.ValuationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Holding>> GetByValuationDateAsync(DateTime valuationDate)
    {
        return await _dbSet
            .Where(h => h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
            .Include(h => h.Portfolio)
            .Include(h => h.Platform)
            .ToListAsync();
    }

    public async Task<IEnumerable<Holding>> GetByPortfolioAndDateRangeAsync(Guid portfolioId, DateTime fromDate, DateTime toDate)
    {
        return await _dbSet
            .Where(h => h.PortfolioId == portfolioId 
                       && h.ValuationDate.Date >= fromDate.Date 
                       && h.ValuationDate.Date <= toDate.Date)
            .Include(h => h.Instrument)
                .ThenInclude(i => i.InstrumentType)
            .Include(h => h.Platform)
            .OrderBy(h => h.ValuationDate)
            .ToListAsync();
    }
}