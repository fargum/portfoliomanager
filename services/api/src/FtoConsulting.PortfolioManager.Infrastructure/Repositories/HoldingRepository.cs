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

    public async Task<IEnumerable<Holding>> GetByPortfolioIdAsync(int portfolioId)
    {
        return await _dbSet
            .Where(h => h.PortfolioId == portfolioId)
            .Include(h => h.Instrument)
                .ThenInclude(i => i.InstrumentType)
            .Include(h => h.Platform)
            .OrderByDescending(h => h.ValuationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Holding>> GetByInstrumentIdAsync(int instrumentId)
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

    public async Task<IEnumerable<Holding>> GetByPortfolioAndDateRangeAsync(int portfolioId, DateTime fromDate, DateTime toDate)
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

    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateTime valuationDate, CancellationToken cancellationToken = default)
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

    public async Task<IEnumerable<string>> GetDistinctTickersByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
            .Select(h => h.Instrument.Ticker)
            .Distinct()
            .OrderBy(ticker => ticker)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(string Ticker, string Name)>> GetDistinctInstrumentsByDateAsync(DateTime valuationDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(h => h.ValuationDate.Date == valuationDate.Date)
            .Include(h => h.Instrument)
            .Select(h => new { h.Instrument.Ticker, h.Instrument.Name })
            .Distinct()
            .OrderBy(x => x.Ticker)
            .Select(x => new ValueTuple<string, string>(x.Ticker, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(int InstrumentId, string Ticker, string Name)>> GetAllDistinctInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(h => h.Instrument)
            .Select(h => new { h.Instrument.Id, h.Instrument.Ticker, h.Instrument.Name })
            .Distinct()
            .OrderBy(x => x.Ticker)
            .Select(x => new ValueTuple<int, string, string>(x.Id, x.Ticker, x.Name))
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

    public async Task<DateOnly?> GetLatestValuationDateBeforeAsync(DateOnly beforeDate, CancellationToken cancellationToken = default)
    {
        var beforeDateTime = DateTime.SpecifyKind(beforeDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        
        var latestDate = await _dbSet
            .Where(h => h.ValuationDate.Date < beforeDateTime.Date)
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
            .OrderBy(h => h.Instrument.Ticker)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get holdings by valuation date without tracking - used for real-time pricing scenarios to prevent persistence
    /// </summary>
    public async Task<IEnumerable<Holding>> GetHoldingsByValuationDateWithInstrumentsNoTrackingAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var targetDate = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        
        return await _dbSet
            .AsNoTracking()
            .Where(h => h.ValuationDate.Date == targetDate.Date)
            .Include(h => h.Instrument)
            .Include(h => h.Portfolio)
            .Include(h => h.Platform)
            .OrderBy(h => h.Instrument.Ticker)
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