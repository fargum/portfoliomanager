using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing instrument price data
/// </summary>
public class InstrumentPriceRepository : Repository<InstrumentPrice>, IInstrumentPriceRepository
{
    public InstrumentPriceRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<InstrumentPrice?> GetByIsinAndDateAsync(string isin, DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        return await _context.Set<InstrumentPrice>()
            .Include(ip => ip.Instrument)
            .FirstOrDefaultAsync(ip => ip.ISIN == isin && ip.ValuationDate == valuationDate, cancellationToken);
    }

    public async Task<IEnumerable<InstrumentPrice>> GetByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        return await _context.Set<InstrumentPrice>()
            .Include(ip => ip.Instrument)
            .Where(ip => ip.ValuationDate == valuationDate)
            .OrderBy(ip => ip.ISIN)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<InstrumentPrice>> GetByIsinAsync(string isin, CancellationToken cancellationToken = default)
    {
        return await _context.Set<InstrumentPrice>()
            .Include(ip => ip.Instrument)
            .Where(ip => ip.ISIN == isin)
            .OrderByDescending(ip => ip.ValuationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteByValuationDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var pricesToDelete = await _context.Set<InstrumentPrice>()
            .Where(ip => ip.ValuationDate == valuationDate)
            .ToListAsync(cancellationToken);

        if (pricesToDelete.Any())
        {
            _context.Set<InstrumentPrice>().RemoveRange(pricesToDelete);
            return pricesToDelete.Count;
        }

        return 0;
    }

    public async Task BulkUpsertAsync(IEnumerable<InstrumentPrice> priceData, CancellationToken cancellationToken = default)
    {
        var priceDataList = priceData.ToList();
        if (!priceDataList.Any()) return;

        // Get the valuation date (assuming all price data is for the same date)
        var valuationDate = priceDataList.First().ValuationDate;

        // Delete existing data for this valuation date
        await DeleteByValuationDateAsync(valuationDate, cancellationToken);

        // Add new data
        await _context.Set<InstrumentPrice>().AddRangeAsync(priceDataList, cancellationToken);
    }
}