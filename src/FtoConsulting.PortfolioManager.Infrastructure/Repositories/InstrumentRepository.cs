using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public class InstrumentRepository : Repository<Instrument>, IInstrumentRepository
{
    public InstrumentRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<Instrument?> GetByISINAsync(string isin)
    {
        return await _dbSet
            .Include(i => i.InstrumentType)
            .FirstOrDefaultAsync(i => i.ISIN == isin);
    }

    public async Task<Instrument?> GetBySEDOLAsync(string sedol)
    {
        return await _dbSet
            .Include(i => i.InstrumentType)
            .FirstOrDefaultAsync(i => i.SEDOL == sedol);
    }

    public async Task<IEnumerable<Instrument>> GetByInstrumentTypeAsync(Guid instrumentTypeId)
    {
        return await _dbSet
            .Where(i => i.InstrumentTypeId == instrumentTypeId)
            .Include(i => i.InstrumentType)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> SearchByNameAsync(string name)
    {
        return await _dbSet
            .Where(i => i.Name.Contains(name))
            .Include(i => i.InstrumentType)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }
}