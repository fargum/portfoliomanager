using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for ingesting portfolio data including holdings and instruments
/// </summary>
public class PortfolioIngestService : IPortfolioIngest
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IHoldingRepository _holdingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PortfolioIngestService> _logger;

    public PortfolioIngestService(
        IPortfolioRepository portfolioRepository,
        IInstrumentRepository instrumentRepository,
        IHoldingRepository holdingRepository,
        IUnitOfWork unitOfWork,
        ILogger<PortfolioIngestService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _instrumentRepository = instrumentRepository;
        _holdingRepository = holdingRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Portfolio> IngestPortfolioAsync(Portfolio portfolio, CancellationToken cancellationToken = default)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));

        _logger.LogInformation("Starting ingestion of portfolio {PortfolioId} with {HoldingCount} holdings", 
            portfolio.Id, portfolio.Holdings?.Count ?? 0);

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            // First, process all instruments in the holdings
            if (portfolio.Holdings?.Any() == true)
            {
                await ProcessInstrumentsAsync(portfolio.Holdings, cancellationToken);
            }

            // Check if portfolio already exists by name and account ID
            var existingPortfolio = await _portfolioRepository.GetByAccountAndNameAsync(portfolio.AccountId, portfolio.Name);
            
            Portfolio resultPortfolio;
            if (existingPortfolio == null)
            {
                // Store holdings separately before saving portfolio (to avoid FK constraint issues)
                var holdingsToProcess = portfolio.Holdings?.ToList() ?? new List<Holding>();
                
                // Clear holdings from portfolio before saving (they have invalid PortfolioId = 0)
                if (portfolio.Holdings != null)
                {
                    portfolio.Holdings.Clear();
                }
                
                // Add new portfolio and save to get the actual ID
                resultPortfolio = await _portfolioRepository.AddAsync(portfolio);
                await _unitOfWork.SaveChangesAsync(); // Save to get the portfolio ID
                _logger.LogInformation("Created new portfolio {PortfolioName} with ID {PortfolioId}", resultPortfolio.Name, resultPortfolio.Id);
                
                // Process holdings with the correct portfolio ID
                if (holdingsToProcess.Any())
                {
                    await ProcessHoldingsAsync(holdingsToProcess, resultPortfolio.Id, cancellationToken);
                }
            }
            else
            {
                // Use existing portfolio
                resultPortfolio = existingPortfolio;
                _logger.LogInformation("Using existing portfolio {PortfolioName} with ID {PortfolioId}", resultPortfolio.Name, resultPortfolio.Id);
                
                
                // Process holdings with the existing portfolio ID
                if (portfolio.Holdings?.Any() == true)
                {
                    await ProcessHoldingsAsync(portfolio.Holdings, resultPortfolio.Id, cancellationToken);
                }
            }

            _logger.LogInformation("Portfolio ingestion completed with instruments and holdings processed");
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Successfully ingested portfolio {PortfolioId}", portfolio.Id);
            
            // Return the portfolio with updated holdings
            return await _portfolioRepository.GetWithHoldingsAsync(resultPortfolio.Id) ?? resultPortfolio;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting portfolio {PortfolioId}", portfolio.Id);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<Portfolio>> IngestPortfoliosAsync(IEnumerable<Portfolio> portfolios, CancellationToken cancellationToken = default)
    {
        if (portfolios == null)
            throw new ArgumentNullException(nameof(portfolios));

        var portfolioList = portfolios.ToList();
        _logger.LogInformation("Starting batch ingestion of {PortfolioCount} portfolios", portfolioList.Count);

        var results = new List<Portfolio>();

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            // Process all instruments first to avoid duplicates
            var allHoldings = portfolioList.SelectMany(p => p.Holdings ?? Enumerable.Empty<Holding>()).ToList();
            if (allHoldings.Any())
            {
                await ProcessInstrumentsAsync(allHoldings, cancellationToken);
            }

            // Process each portfolio
            foreach (var portfolio in portfolioList)
            {
                var processedPortfolio = await ProcessSinglePortfolioInTransaction(portfolio, cancellationToken);
                results.Add(processedPortfolio);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Successfully ingested {PortfolioCount} portfolios", portfolioList.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting batch of {PortfolioCount} portfolios", portfolioList.Count);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task ProcessInstrumentsAsync(IEnumerable<Holding> holdings, CancellationToken cancellationToken)
    {
        // Get unique instruments by ticker
        var uniqueInstruments = holdings
            .Where(h => h.Instrument != null && !string.IsNullOrEmpty(h.Instrument.Ticker))
            .GroupBy(h => h.Instrument!.Ticker)
            .Select(g => g.First().Instrument!)
            .ToList();

        _logger.LogDebug("Processing {InstrumentCount} unique instruments", uniqueInstruments.Count);

        foreach (var instrument in uniqueInstruments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if instrument already exists by ticker
            var existingInstrument = await _instrumentRepository.GetByTickerAsync(instrument.Ticker);
            
            if (existingInstrument == null)
            {
                // Create new instrument
                await _instrumentRepository.AddAsync(instrument);
                _logger.LogDebug("Created new instrument {Ticker} - {Name}", instrument.Ticker, instrument.Name);
            }
            else
            {
                // Update existing instrument if needed
                if (ShouldUpdateInstrument(existingInstrument, instrument))
                {
                    existingInstrument.UpdateDetails(instrument.Name, instrument.Ticker, instrument.Description, instrument.CurrencyCode, instrument.QuoteUnit);
                    if (existingInstrument.InstrumentTypeId != instrument.InstrumentTypeId)
                    {
                        existingInstrument.UpdateInstrumentType(instrument.InstrumentTypeId);
                    }
                    
                    await _instrumentRepository.UpdateAsync(existingInstrument);
                    _logger.LogDebug("Updated existing instrument {Ticker}", instrument.Ticker);
                }
            }
        }

        // Save all instrument changes before processing holdings
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ProcessHoldingsAsync(IEnumerable<Holding> holdings, int portfolioId, CancellationToken cancellationToken)
    {
        // Process each holding and map instruments by ticker directly
        foreach (var holding in holdings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (holding.Instrument == null || string.IsNullOrEmpty(holding.Instrument.Ticker))
            {
                _logger.LogError("Holding has no instrument or ticker information");
                continue;
            }

            // Look up the actual instrument by Ticker (instruments should have been processed already)
            var actualInstrument = await _instrumentRepository.GetByTickerAsync(holding.Instrument.Ticker);
            if (actualInstrument == null)
            {
                _logger.LogError("Could not find instrument with Ticker {Ticker} in database", holding.Instrument.Ticker);
                continue;
            }

            // Create a new holding instance with the correct instrument ID and portfolio ID
            var newHolding = new Holding(
                holding.ValuationDate,
                actualInstrument.Id,
                holding.PlatformId,
                portfolioId,
                holding.UnitAmount,
                holding.BoughtValue,
                holding.CurrentValue);
            
            // Set daily P&L if it was provided in the original holding
            if (holding.DailyProfitLoss != 0 || holding.DailyProfitLossPercentage != 0)
            {
                newHolding.SetDailyProfitLoss(holding.DailyProfitLoss, holding.DailyProfitLossPercentage);
            }

            await _holdingRepository.AddAsync(newHolding);
            _logger.LogDebug("Added holding for instrument {Ticker} to portfolio {PortfolioId}", 
                holding.Instrument.Ticker, portfolioId);
        }

        // Save all holding changes
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Portfolio> ProcessSinglePortfolioInTransaction(Portfolio portfolio, CancellationToken cancellationToken)
    {
        // Check if portfolio already exists
        var existingPortfolio = await _portfolioRepository.GetByIdAsync(portfolio.Id);
        
        Portfolio resultPortfolio;
        if (existingPortfolio == null)
        {
            resultPortfolio = await _portfolioRepository.AddAsync(portfolio);
        }
        else
        {
            existingPortfolio.UpdateName(portfolio.Name);
            await _portfolioRepository.UpdateAsync(existingPortfolio);
            resultPortfolio = existingPortfolio;
        }

        // Process holdings (create a copy to avoid collection modification during enumeration)
        if (portfolio.Holdings?.Any() == true)
        {
            var holdingsList = portfolio.Holdings.ToList();
            await ProcessHoldingsAsync(holdingsList, resultPortfolio.Id, cancellationToken);
        }

        return resultPortfolio;
    }

    private static bool ShouldUpdateInstrument(Instrument existing, Instrument incoming)
    {
        return existing.Name != incoming.Name ||
               existing.Description != incoming.Description ||
               existing.Ticker != incoming.Ticker ||
               existing.CurrencyCode != incoming.CurrencyCode ||
               existing.QuoteUnit != incoming.QuoteUnit ||
               existing.InstrumentTypeId != incoming.InstrumentTypeId;
    }
}
