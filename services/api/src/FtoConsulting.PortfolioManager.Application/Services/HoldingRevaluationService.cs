using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Constants;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for revaluing holdings based on current market prices
/// </summary>
public class HoldingRevaluationService : IHoldingRevaluationService
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly IInstrumentPriceRepository _instrumentPriceRepository;
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly IPricingCalculationService _pricingCalculationService;
    private readonly IPriceFetching _priceFetchingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HoldingRevaluationService> _logger;

    public HoldingRevaluationService(
        IHoldingRepository holdingRepository,
        IInstrumentPriceRepository instrumentPriceRepository,
        ICurrencyConversionService currencyConversionService,
        IPricingCalculationService pricingCalculationService,
        IPriceFetching priceFetchingService,
        IUnitOfWork unitOfWork,
        ILogger<HoldingRevaluationService> logger)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _instrumentPriceRepository = instrumentPriceRepository ?? throw new ArgumentNullException(nameof(instrumentPriceRepository));
        _currencyConversionService = currencyConversionService ?? throw new ArgumentNullException(nameof(currencyConversionService));
        _pricingCalculationService = pricingCalculationService ?? throw new ArgumentNullException(nameof(pricingCalculationService));
        _priceFetchingService = priceFetchingService ?? throw new ArgumentNullException(nameof(priceFetchingService));
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
            var revaluedHoldings = await ProcessHoldingsAsync(sourceHoldings, priceDict, valuationDate, result);

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
        // Get the latest available valuation date that is before the target revaluation date
        var latestValuationDate = await _holdingRepository.GetLatestValuationDateBeforeAsync(result.ValuationDate, cancellationToken);
        if (latestValuationDate == null)
        {
            _logger.LogWarning("No holdings found in database before {TargetDate} - cannot perform revaluation", result.ValuationDate);
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
        var priceDict = priceData
            .Where(p => !string.IsNullOrEmpty(p.Ticker))
            .ToDictionary(p => p.Ticker!, p => p);

        _logger.LogInformation("Found price data for {PriceCount} instruments on {ValuationDate}", 
            priceDict.Count, valuationDate);

        return priceDict;
    }

    private async Task<List<Holding>> ProcessHoldingsAsync(
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
                var revaluedHolding = await ProcessSingleHoldingAsync(sourceHolding, priceDict, valuationDate, result);
                if (revaluedHolding != null)
                {
                    revaluedHoldings.Add(revaluedHolding);
                    result.SuccessfulRevaluations++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revaluing holding for instrument {Ticker} ({Name})", 
                    sourceHolding.Instrument.Ticker, sourceHolding.Instrument.Name);
                
                result.FailedInstruments.Add(new FailedRevaluationData
                {
                    Ticker = sourceHolding.Instrument.Ticker,
                    InstrumentName = sourceHolding.Instrument.Name,
                    ErrorMessage = ex.Message,
                    ErrorCode = "CALCULATION_ERROR"
                });
            }
        }

        result.FailedRevaluations = result.FailedInstruments.Count;
        return revaluedHoldings;
    }

    private async Task<Holding?> ProcessSingleHoldingAsync(
        Holding sourceHolding, 
        Dictionary<string, InstrumentPrice> priceDict, 
        DateOnly valuationDate, 
        HoldingRevaluationResult result)
    {
        // Check if this is a CASH instrument - if so, roll forward without pricing
        if (sourceHolding.Instrument.Ticker.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Rolling forward CASH holding for {Ticker} without pricing - Source: UnitAmount={UnitAmount}, CurrentValue={CurrentValue}, BoughtValue={BoughtValue}", 
                sourceHolding.Instrument.Ticker, sourceHolding.UnitAmount, sourceHolding.CurrentValue, sourceHolding.BoughtValue);
            
            // For CASH holdings, current value should always equal unit amount (1:1 ratio)
            // Fix any corrupted CASH holdings where current value doesn't match unit amount
            var correctedCurrentValue = sourceHolding.UnitAmount;
            var cashDailyChange = correctedCurrentValue - sourceHolding.CurrentValue;
            var cashDailyChangePercentage = sourceHolding.CurrentValue != 0 
                ? (cashDailyChange / sourceHolding.CurrentValue) * 100 
                : 0;

            var cashHolding = new Holding(
                DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                sourceHolding.InstrumentId,
                sourceHolding.PlatformId,
                sourceHolding.PortfolioId,
                sourceHolding.UnitAmount,
                sourceHolding.BoughtValue,
                correctedCurrentValue // Use unit amount as current value for CASH
            );

            // Set the daily profit/loss to reflect the correction
            cashHolding.SetDailyProfitLoss(cashDailyChange, cashDailyChangePercentage);
            
            _logger.LogInformation("Created rolled forward CASH holding - Target: UnitAmount={UnitAmount}, CurrentValue={CurrentValue}, BoughtValue={BoughtValue}, DailyChange={DailyChange}", 
                cashHolding.UnitAmount, cashHolding.CurrentValue, cashHolding.BoughtValue, cashDailyChange);
            
            if (cashDailyChange != 0)
            {
                _logger.LogWarning("Corrected CASH holding value from {OldValue} to {NewValue} (difference: {Correction})", 
                    sourceHolding.CurrentValue, correctedCurrentValue, cashDailyChange);
            }
            
            return cashHolding;
        }

        // Get price for this instrument
        if (!priceDict.TryGetValue(sourceHolding.Instrument.Ticker, out var instrumentPrice))
        {
            _logger.LogInformation("No price data found for instrument {Ticker} ({Name}) on {ValuationDate}, rolling forward holding with previous valuation",
                sourceHolding.Instrument.Ticker, sourceHolding.Instrument.Name, valuationDate);

            // Roll forward the holding with the same valuation as the source date
            var rolledForwardHolding = CreateRolledForwardHolding(sourceHolding, valuationDate);

            _logger.LogInformation("Successfully rolled forward holding for {Ticker}: UnitAmount={UnitAmount}, CurrentValue={CurrentValue} (unchanged from {SourceDate})",
                sourceHolding.Instrument.Ticker, sourceHolding.UnitAmount, sourceHolding.CurrentValue, DateOnly.FromDateTime(sourceHolding.ValuationDate));

            return rolledForwardHolding;
        }

        // Apply scaling factor for proxy instruments using shared service
        var scaledPrice = _pricingCalculationService.ApplyScalingFactor(instrumentPrice.Price, sourceHolding.Instrument.Ticker);

        
// Calculate current value considering quote unit and currency conversion
        var currentValue = await _pricingCalculationService.CalculateCurrentValueAsync(
            sourceHolding.UnitAmount, 
            scaledPrice, // Use scaled price instead of raw price
            sourceHolding.Instrument.QuoteUnit,
            instrumentPrice.Currency,
            valuationDate);

        // Calculate daily change (difference from previous current value)
        var (dailyChange, dailyChangePercentage) = CalculateDailyChange(currentValue, sourceHolding.CurrentValue);

        // Create new holding for the target valuation date
        var revaluedHolding = CreateRevaluedHolding(sourceHolding, valuationDate, currentValue, dailyChange, dailyChangePercentage);

        _logger.LogDebug("Revalued holding for {Ticker}: UnitAmount={UnitAmount}, Price={Price}, ScaledPrice={ScaledPrice}, CurrentValue={CurrentValue}, DailyChange={DailyChange}", 
            sourceHolding.Instrument.Ticker, sourceHolding.UnitAmount, instrumentPrice.Price, scaledPrice, currentValue, dailyChange);

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
    /// Creates a rolled forward holding with the same valuation but updated valuation date
    /// </summary>
    /// <param name="sourceHolding">The source holding to roll forward</param>
    /// <param name="newValuationDate">The new valuation date</param>
    /// <returns>A new holding with the same values but updated date</returns>
    private static Holding CreateRolledForwardHolding(Holding sourceHolding, DateOnly newValuationDate)
    {
        var rolledForwardHolding = new Holding(
            DateTime.SpecifyKind(newValuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            sourceHolding.InstrumentId,
            sourceHolding.PlatformId,
            sourceHolding.PortfolioId,
            sourceHolding.UnitAmount,
            sourceHolding.BoughtValue,
            sourceHolding.CurrentValue // Keep the same current value from source
        );

        // Set daily profit/loss to zero since value hasn't changed
        rolledForwardHolding.SetDailyProfitLoss(0m, 0m);

        return rolledForwardHolding;
    }

    public async Task<CombinedPriceAndRevaluationResult> FetchPricesAndRevalueHoldingsAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var result = new CombinedPriceAndRevaluationResult
        {
            ValuationDate = valuationDate,
            ProcessedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Starting combined price fetch and revaluation for date {ValuationDate}", valuationDate);

        try
        {
            // Step 1: Fetch market prices first
            _logger.LogInformation("Step 1: Fetching market prices for {ValuationDate}", valuationDate);
            result.PriceFetchResult = await _priceFetchingService.FetchAndPersistPricesForDateAsync(valuationDate, cancellationToken);

            _logger.LogInformation("Price fetch completed: Success={Success}, Failed={Failed}, Duration={Duration}ms", 
                result.PriceFetchResult.SuccessfulPrices, 
                result.PriceFetchResult.FailedPrices, 
                result.PriceFetchResult.FetchDuration.TotalMilliseconds);

            // Step 2: Revalue holdings using the fetched prices
            _logger.LogInformation("Step 2: Revaluing holdings for {ValuationDate}", valuationDate);
            
result.HoldingRevaluationResult = await RevalueHoldingsAsync(valuationDate, cancellationToken);

            _logger.LogInformation("Revaluation completed: Success={Success}, Failed={Failed}, Duration={Duration}ms",
                result.HoldingRevaluationResult.SuccessfulRevaluations,
                result.HoldingRevaluationResult.FailedRevaluations,
                result.HoldingRevaluationResult.Duration.TotalMilliseconds);

            _logger.LogInformation("Combined operation completed successfully. {Summary}", result.Summary);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during combined price fetch and revaluation for date {ValuationDate}", valuationDate);
            throw;
        }
    }
}
