using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for revaluing holdings based on current market prices
/// </summary>
public class HoldingRevaluationService : IHoldingRevaluationService
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly IInstrumentPriceRepository _instrumentPriceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HoldingRevaluationService> _logger;

    public HoldingRevaluationService(
        IHoldingRepository holdingRepository,
        IInstrumentPriceRepository instrumentPriceRepository,
        IUnitOfWork unitOfWork,
        ILogger<HoldingRevaluationService> logger)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _instrumentPriceRepository = instrumentPriceRepository ?? throw new ArgumentNullException(nameof(instrumentPriceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HoldingRevaluationResult> RevalueHoldingsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new HoldingRevaluationResult
        {
            ValuationDate = valuationDate,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting holding revaluation for valuation date {ValuationDate}", valuationDate);

            // Step 1: Get source holdings data
            var sourceHoldings = await GetSourceHoldingsAsync(result, cancellationToken);
            if (sourceHoldings == null)
            {
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Step 2: Prepare target date (remove existing holdings if any)
            await PrepareTargetDateAsync(valuationDate, result, cancellationToken);

            // Step 3: Get price data for revaluation
            var priceDict = await GetPriceDataAsync(valuationDate, cancellationToken);

            // Step 4: Process holdings and create revalued versions
            var revaluedHoldings = ProcessHoldings(sourceHoldings, priceDict, valuationDate, result);

            // Step 5: Save revalued holdings to database
            await SaveRevaluedHoldingsAsync(revaluedHoldings, valuationDate, cancellationToken);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Holding revaluation completed. Success: {SuccessCount}, Failed: {FailedCount}, Duration: {Duration}ms",
                result.SuccessfulRevaluations, result.FailedRevaluations, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            _logger.LogError(ex, "Error during holding revaluation for date {ValuationDate}", valuationDate);
            throw;
        }
    }

    private async Task<List<Holding>?> GetSourceHoldingsAsync(HoldingRevaluationResult result, CancellationToken cancellationToken)
    {
        // Get the latest available valuation date
        var latestValuationDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
        if (latestValuationDate == null)
        {
            _logger.LogWarning("No holdings found in database - cannot perform revaluation");
            return null;
        }

        result.SourceValuationDate = latestValuationDate;
        _logger.LogInformation("Using source valuation date {SourceDate} for revaluation to {TargetDate}", 
            latestValuationDate, result.ValuationDate);

        // Get holdings from the latest valuation date
        var sourceHoldings = await _holdingRepository.GetHoldingsByValuationDateWithInstrumentsAsync(
            latestValuationDate.Value, cancellationToken);
        var sourceHoldingsList = sourceHoldings.ToList();

        result.TotalHoldings = sourceHoldingsList.Count;

        if (!sourceHoldingsList.Any())
        {
            _logger.LogWarning("No holdings found for source valuation date {SourceDate}", latestValuationDate);
            return null;
        }

        _logger.LogInformation("Found {Count} holdings to revalue from {SourceDate}", 
            sourceHoldingsList.Count, latestValuationDate);

        return sourceHoldingsList;
    }

    private async Task PrepareTargetDateAsync(DateOnly valuationDate, HoldingRevaluationResult result, CancellationToken cancellationToken)
    {
        // Check if holdings already exist for target date and remove them
        var existingHoldings = await _holdingRepository.GetHoldingsByValuationDateWithInstrumentsAsync(
            valuationDate, cancellationToken);
        var existingHoldingsList = existingHoldings.ToList();

        if (existingHoldingsList.Any())
        {
            _logger.LogInformation("Found {Count} existing holdings for {ValuationDate} - will replace them", 
                existingHoldingsList.Count, valuationDate);
            
            await _holdingRepository.DeleteHoldingsByValuationDateAsync(valuationDate, cancellationToken);
            result.ReplacedHoldings = existingHoldingsList.Count;
        }
    }

    private async Task<Dictionary<string, InstrumentPrice>> GetPriceDataAsync(DateOnly valuationDate, CancellationToken cancellationToken)
    {
        var priceData = await _instrumentPriceRepository.GetByValuationDateAsync(valuationDate, cancellationToken);
        var priceDict = priceData.ToDictionary(p => p.ISIN, p => p);

        _logger.LogInformation("Found price data for {PriceCount} instruments on {ValuationDate}", 
            priceDict.Count, valuationDate);

        return priceDict;
    }

    private List<Holding> ProcessHoldings(
        List<Holding> sourceHoldings, 
        Dictionary<string, InstrumentPrice> priceDict, 
        DateOnly valuationDate, 
        HoldingRevaluationResult result)
    {
        var revaluedHoldings = new List<Holding>();

        foreach (var sourceHolding in sourceHoldings)
        {
            try
            {
                var revaluedHolding = ProcessSingleHolding(sourceHolding, priceDict, valuationDate, result);
                if (revaluedHolding != null)
                {
                    revaluedHoldings.Add(revaluedHolding);
                    result.SuccessfulRevaluations++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revaluing holding for instrument {ISIN} ({Name})", 
                    sourceHolding.Instrument.ISIN, sourceHolding.Instrument.Name);
                
                result.FailedInstruments.Add(new FailedRevaluationData
                {
                    ISIN = sourceHolding.Instrument.ISIN,
                    InstrumentName = sourceHolding.Instrument.Name,
                    ErrorMessage = ex.Message,
                    ErrorCode = "CALCULATION_ERROR"
                });
            }
        }

        result.FailedRevaluations = result.FailedInstruments.Count;
        return revaluedHoldings;
    }

    private Holding? ProcessSingleHolding(
        Holding sourceHolding, 
        Dictionary<string, InstrumentPrice> priceDict, 
        DateOnly valuationDate, 
        HoldingRevaluationResult result)
    {
        // Get price for this instrument
        if (!priceDict.TryGetValue(sourceHolding.Instrument.ISIN, out var instrumentPrice))
        {
            _logger.LogWarning("No price data found for instrument {ISIN} ({Name}) on {ValuationDate}", 
                sourceHolding.Instrument.ISIN, sourceHolding.Instrument.Name, valuationDate);
            
            result.FailedInstruments.Add(new FailedRevaluationData
            {
                ISIN = sourceHolding.Instrument.ISIN,
                InstrumentName = sourceHolding.Instrument.Name,
                ErrorMessage = "No price data available",
                ErrorCode = "NO_PRICE_DATA"
            });
            return null;
        }

        // Calculate current value considering quote unit
        var currentValue = CalculateCurrentValue(
            sourceHolding.UnitAmount, 
            instrumentPrice.Price, 
            sourceHolding.Instrument.QuoteUnit);

        // Calculate daily change (difference from previous current value)
        var (dailyChange, dailyChangePercentage) = CalculateDailyChange(currentValue, sourceHolding.CurrentValue);

        // Create new holding for the target valuation date
        var revaluedHolding = CreateRevaluedHolding(sourceHolding, valuationDate, currentValue, dailyChange, dailyChangePercentage);

        _logger.LogDebug("Revalued holding for {ISIN}: UnitAmount={UnitAmount}, Price={Price}, CurrentValue={CurrentValue}, DailyChange={DailyChange}", 
            sourceHolding.Instrument.ISIN, sourceHolding.UnitAmount, instrumentPrice.Price, currentValue, dailyChange);

        return revaluedHolding;
    }

    private static (decimal dailyChange, decimal dailyChangePercentage) CalculateDailyChange(decimal currentValue, decimal previousValue)
    {
        var dailyChange = currentValue - previousValue;
        var dailyChangePercentage = previousValue != 0 
            ? (dailyChange / previousValue) * 100 
            : 0;

        return (dailyChange, dailyChangePercentage);
    }

    private static Holding CreateRevaluedHolding(
        Holding sourceHolding, 
        DateOnly valuationDate, 
        decimal currentValue, 
        decimal dailyChange, 
        decimal dailyChangePercentage)
    {
        var revaluedHolding = new Holding(
            DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            sourceHolding.InstrumentId,
            sourceHolding.PlatformId,
            sourceHolding.PortfolioId,
            sourceHolding.UnitAmount,
            sourceHolding.BoughtValue, // Keep original book cost
            currentValue
        );

        // Set the daily profit/loss
        revaluedHolding.SetDailyProfitLoss(dailyChange, dailyChangePercentage);

        return revaluedHolding;
    }

    private async Task SaveRevaluedHoldingsAsync(List<Holding> revaluedHoldings, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        if (!revaluedHoldings.Any())
            return;

        _logger.LogInformation("Saving {Count} revalued holdings to database for {ValuationDate}", 
            revaluedHoldings.Count, valuationDate);

        foreach (var holding in revaluedHoldings)
        {
            await _holdingRepository.AddAsync(holding);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Successfully saved {Count} revalued holdings for {ValuationDate}", 
            revaluedHoldings.Count, valuationDate);
    }

    /// <summary>
    /// Calculate current value considering quote unit conversion
    /// </summary>
    /// <param name="unitAmount">Number of shares/units</param>
    /// <param name="price">Price per unit</param>
    /// <param name="quoteUnit">Quote unit (GBP, GBX, etc.)</param>
    /// <returns>Current value in GBP</returns>
    private static decimal CalculateCurrentValue(decimal unitAmount, decimal price, string? quoteUnit)
    {
        // Default to GBP if no quote unit specified
        var effectiveQuoteUnit = quoteUnit?.ToUpperInvariant() ?? "GBP";
        
        decimal adjustedPrice = effectiveQuoteUnit switch
        {
            "GBX" => price / 100m, // Convert pence to pounds (100 pence = 1 pound)
            "GBP" => price,        // Already in pounds
            _ => price             // Default to no conversion for unknown units
        };

        return unitAmount * adjustedPrice;
    }
}