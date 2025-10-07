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

    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(Guid accountId, DateTime valuationDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.Portfolio.AccountId == accountId && h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
                .ThenInclude(i => i.InstrumentType)
            .Include(h => h.Platform)
            .Include(h => h.Portfolio)
                .ThenInclude(p => p.Account)
            .OrderBy(h => h.Portfolio.Name)
            .ThenBy(h => h.Instrument.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetDistinctIsinsByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
            .Select(h => h.Instrument.ISIN)
            .Distinct()
            .OrderBy(isin => isin)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(string ISIN, string? Ticker)>> GetDistinctInstrumentsByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
            .Select(h => new { h.Instrument.ISIN, h.Instrument.Ticker })
            .Distinct()
            .OrderBy(x => x.ISIN)
            .Select(x => new ValueTuple<string, string?>(x.ISIN, x.Ticker))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(string ISIN, string? Ticker)>> GetAllDistinctInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(h => h.Instrument)
            .Select(h => new { h.Instrument.ISIN, h.Instrument.Ticker })
            .Distinct()
            .OrderBy(x => x.ISIN)
            .Select(x => new ValueTuple<string, string?>(x.ISIN, x.Ticker))
            .ToListAsync(cancellationToken);
    }

    public async Task<DateOnly?> GetLatestValuationDateAsync(CancellationToken cancellationToken = default)
    {
        var latestDate = await _dbSet
            .OrderByDescending(h => h.ValuationDate)
            .Select(h => h.ValuationDate)
            .FirstOrDefaultAsync(cancellationToken);
        
        return latestDate == default ? null : DateOnly.FromDateTime(latestDate);
    }

    public async Task<IEnumerable<Holding>> GetHoldingsByValuationDateWithInstrumentsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var targetDate = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        
        return await _dbSet
            .Where(h => h.ValuationDate.Date == targetDate.Date)
            .Include(h => h.Instrument)
            .Include(h => h.Portfolio)
            .Include(h => h.Platform)
            .OrderBy(h => h.Instrument.ISIN)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteHoldingsByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var targetDate = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        
        var holdingsToDelete = await _dbSet
            .Where(h => h.ValuationDate.Date == targetDate.Date)
            .ToListAsync(cancellationToken);
        
        if (holdingsToDelete.Any())
        {
            _dbSet.RemoveRange(holdingsToDelete);
        }
    }
}