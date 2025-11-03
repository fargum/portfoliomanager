using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing exchange rate data
/// </summary>
public class ExchangeRateRepository : Repository<ExchangeRate>, IExchangeRateRepository
{
    public ExchangeRateRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<ExchangeRate?> GetLatestRateAsync(string baseCurrency, string targetCurrency, DateOnly onOrBeforeDate, CancellationToken cancellationToken = default)
    {
        baseCurrency = baseCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(baseCurrency));
        targetCurrency = targetCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(targetCurrency));

        return await _context.Set<ExchangeRate>()
            .Where(er => er.BaseCurrency == baseCurrency && 
                        er.TargetCurrency == targetCurrency && 
                        er.RateDate <= onOrBeforeDate)
            .OrderByDescending(er => er.RateDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<ExchangeRate>> GetRatesByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ExchangeRate>()
            .Where(er => er.RateDate == rateDate)
            .OrderBy(er => er.BaseCurrency)
            .ThenBy(er => er.TargetCurrency)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken = default)
    {
        var ratesList = rates.ToList();
        if (!ratesList.Any()) return;

        // Get the rate date (assuming all rates are for the same date)
        var rateDate = ratesList.First().RateDate;

        // Delete existing data for this rate date
        await DeleteByRateDateAsync(rateDate, cancellationToken);

        // Add new data
        await _context.Set<ExchangeRate>().AddRangeAsync(ratesList, cancellationToken);
    }

    public async Task<IEnumerable<ExchangeRate>> GetRateHistoryAsync(string baseCurrency, string targetCurrency, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        baseCurrency = baseCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(baseCurrency));
        targetCurrency = targetCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(targetCurrency));

        return await _context.Set<ExchangeRate>()
            .Where(er => er.BaseCurrency == baseCurrency && 
                        er.TargetCurrency == targetCurrency && 
                        er.RateDate >= fromDate && 
                        er.RateDate <= toDate)
            .OrderBy(er => er.RateDate)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> DeleteByRateDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        var ratesToDelete = await _context.Set<ExchangeRate>()
            .Where(er => er.RateDate == rateDate)
            .ToListAsync(cancellationToken);

        if (ratesToDelete.Any())
        {
            _context.Set<ExchangeRate>().RemoveRange(ratesToDelete);
            return ratesToDelete.Count;
        }

        return 0;
    }
}