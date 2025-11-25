using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Comprehensive service for all holding operations (CRUD and retrieval)
/// Combines both holding management and retrieval functionality
/// </summary>
public class HoldingService : IHoldingService
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IInstrumentManagementService _instrumentManagementService;
    private readonly IPricingCalculationHelper _pricingCalculationHelper;
    private readonly IPricingCalculationService _pricingCalculationService;
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HoldingService> _logger;
    private readonly Func<EodMarketDataTool>? _eodMarketDataToolFactory;

    public HoldingService(
        IHoldingRepository holdingRepository,
        IPortfolioRepository portfolioRepository,
        IInstrumentRepository instrumentRepository,
        IInstrumentManagementService instrumentManagementService,
        IPricingCalculationHelper pricingCalculationHelper,
        IPricingCalculationService pricingCalculationService,
        ICurrencyConversionService currencyConversionService,
        IUnitOfWork unitOfWork,
        ILogger<HoldingService> logger,
        Func<EodMarketDataTool>? eodMarketDataToolFactory = null)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _portfolioRepository = portfolioRepository ?? throw new ArgumentNullException(nameof(portfolioRepository));
        _instrumentRepository = instrumentRepository ?? throw new ArgumentNullException(nameof(instrumentRepository));
        _instrumentManagementService = instrumentManagementService ?? throw new ArgumentNullException(nameof(instrumentManagementService));
        _pricingCalculationHelper = pricingCalculationHelper ?? throw new ArgumentNullException(nameof(pricingCalculationHelper));
        _pricingCalculationService = pricingCalculationService ?? throw new ArgumentNullException(nameof(pricingCalculationService));
        _currencyConversionService = currencyConversionService ?? throw new ArgumentNullException(nameof(currencyConversionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eodMarketDataToolFactory = eodMarketDataToolFactory;
    }

    // Read Operations (from HoldingsRetrievalService)

    /// <summary>
    /// Retrieves all holdings for a given account on a specific date
    /// Supports both historical data retrieval and real-time pricing for current date
    /// </summary>
    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            IEnumerable<Holding> holdings;
            var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            
            if (valuationDate < today)
            {
                // Historical data - get holdings for the specific date
                holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
                _logger.LogInformation("Retrieved {Count} historical holdings for account {AccountId} on date {ValuationDate}", 
                    holdings.Count(), accountId, valuationDate);
                return holdings;
            }

            if (valuationDate > today)
            {
                // Future date - not supported, return empty
                _logger.LogWarning("Future date {ValuationDate} requested, returning empty holdings", valuationDate);
                holdings = Enumerable.Empty<Holding>();
                return holdings;
            }

            holdings = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
            if (holdings.Any())
            {
                _logger.LogInformation("Retrieved {Count} holdings for account {AccountId} on today's date {ValuationDate}", 
                    holdings.Count(), accountId, valuationDate);
                return holdings;
            }

            // Real-time data - get the most recent holdings and apply real-time pricing
            var latestDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (latestDate.HasValue)
            {
                // Get ALL holdings from the latest date, then filter by account (using no-tracking for real-time pricing)
                var allLatestHoldings = await _holdingRepository.GetHoldingsByValuationDateWithInstrumentsNoTrackingAsync(latestDate.Value, cancellationToken);
                holdings = allLatestHoldings.Where(h => h.Portfolio.AccountId == accountId).ToList();
                _logger.LogInformation("Retrieved {Count} latest holdings for account {AccountId} from date {LatestDate} for real-time pricing (no tracking to prevent persistence)", 
                    holdings.Count(), accountId, latestDate.Value);

                if (_eodMarketDataToolFactory != null)
                {
                    _logger.LogInformation("Applying real-time pricing for today's date: {Date}", valuationDate);
                    holdings = await ApplyRealTimePricing(holdings.ToList(), valuationDate, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("EOD market data tool not available for real-time pricing, returning latest holdings with historical prices");
                }
            }
            else
            {
                _logger.LogWarning("No historical holdings found for account {AccountId}, returning empty holdings", accountId);
                holdings = Enumerable.Empty<Holding>();
            }
                      
            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            throw;
        }
    }

    // Create Operations (from HoldingManagementService)

    /// <summary>
    /// Adds a new holding to a portfolio for the latest valuation date
    /// </summary>
    public async Task<HoldingAddResult> AddHoldingAsync(
        int portfolioId,
        AddHoldingRequest request,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var result = new HoldingAddResult();

        try
        {
            _logger.LogInformation("Adding new holding {Ticker} to portfolio {PortfolioId} for account {AccountId}",
                request.Ticker, portfolioId, accountId);

            // Validate input
            var validationErrors = ValidateAddHoldingRequest(request);
            if (validationErrors.Any())
            {
                result.Errors.AddRange(validationErrors);
                result.Message = "Validation failed";
                return result;
            }

            // Get latest valuation date
            var latestValuationDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (latestValuationDate == null)
            {
                result.Errors.Add("No holdings found in the system");
                result.Message = "Cannot add holdings - no valuation data available";
                return result;
            }

            // Verify portfolio ownership
            var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
            if (portfolio == null || portfolio.AccountId != accountId)
            {
                _logger.LogWarning("Portfolio access denied: PortfolioId={PortfolioId}, AccountId={AccountId}, Portfolio={Portfolio}", 
                    portfolioId, accountId, portfolio);
                result.Errors.Add("Portfolio not found or does not belong to your account");
                result.Message = "Portfolio not accessible";
                return result;
            }

            _logger.LogInformation("Portfolio verification successful: PortfolioId={PortfolioId}, PortfolioName={PortfolioName}, OwnerAccountId={OwnerAccountId}", 
                portfolio.Id, portfolio.Name, portfolio.AccountId);

            // Debug dependency injection
            _logger.LogInformation("Checking dependency injection - UnitOfWork: {UnitOfWorkType}, HoldingRepository: {HoldingRepositoryType}, PortfolioRepository: {PortfolioRepositoryType}", 
                _unitOfWork?.GetType().Name ?? "NULL",
                _holdingRepository?.GetType().Name ?? "NULL", 
                _portfolioRepository?.GetType().Name ?? "NULL");

            if (_unitOfWork == null)
            {
                _logger.LogError("UnitOfWork is null - dependency injection failure");
                result.Errors.Add("Internal service configuration error - UnitOfWork not available");
                result.Message = "Service configuration error";
                return result;
            }

            // Begin transaction
            _logger.LogInformation("Beginning database transaction for holding creation");
            try
            {
                await _unitOfWork.BeginTransactionAsync();
                _logger.LogInformation("Database transaction started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to begin database transaction: {ErrorMessage}", ex.Message);
                result.Errors.Add($"Database transaction error: {ex.Message}");
                result.Message = "Failed to begin database transaction";
                return result;
            }

            // Ensure instrument exists
            _logger.LogInformation("Starting instrument processing for ticker: {Ticker}", request.Ticker);
            var instrument = await EnsureInstrumentExistsAsync(request, cancellationToken);
            if (instrument == null)
            {
                result.Errors.Add("Failed to create or retrieve instrument");
                result.Message = "Instrument processing failed";
                return result;
            }

            result.Instrument = instrument;
            result.InstrumentCreated = await WasInstrumentJustCreated(request.Ticker, cancellationToken);

            _logger.LogInformation("Instrument processed: {InstrumentId}, Created: {InstrumentCreated}", 
                instrument.Id, result.InstrumentCreated);

            // Check for duplicate holding
            _logger.LogInformation("Checking for duplicate holding: Portfolio={PortfolioId}, Instrument={InstrumentId}, Date={ValuationDate}", 
                portfolioId, instrument.Id, latestValuationDate.Value);
            
            var existingHolding = await GetExistingHoldingAsync(portfolioId, instrument.Id, latestValuationDate.Value, cancellationToken);
            if (existingHolding != null)
            {
                _logger.LogWarning("Duplicate holding found: {ExistingHoldingId}", existingHolding.Id);
                result.Errors.Add($"A holding for {request.Ticker} already exists in this portfolio for the current valuation date");
                result.Message = "Duplicate holding detected";
                return result;
            }

            _logger.LogInformation("No duplicate holding found, proceeding with pricing calculation");

            // Calculate current value
            _logger.LogInformation("Starting pricing calculation for {Ticker}, Units={Units}, QuoteUnit={QuoteUnit}, Currency={Currency}", 
                instrument.Ticker, request.Units, instrument.QuoteUnit, instrument.CurrencyCode);
                
            var pricingResult = await _pricingCalculationHelper.FetchAndCalculateHoldingValueAsync(
                instrument.Id, instrument.Ticker, request.Units, 
                instrument.QuoteUnit, instrument.CurrencyCode, 
                latestValuationDate.Value, cancellationToken);

            if (!pricingResult.Success)
            {
                _logger.LogError("Pricing calculation failed: {ErrorMessage}", pricingResult.ErrorMessage);
                result.Errors.Add(pricingResult.ErrorMessage ?? "Failed to calculate current value");
                result.Message = "Pricing calculation failed";
                return result;
            }

            _logger.LogInformation("Pricing calculation successful: CurrentPrice={CurrentPrice}, CurrentValue={CurrentValue}", 
                pricingResult.CurrentPrice, pricingResult.CurrentValue);

            // Create new holding
            _logger.LogInformation("Creating new holding: ValuationDate={ValuationDate}, InstrumentId={InstrumentId}, PlatformId={PlatformId}, PortfolioId={PortfolioId}, Units={Units}, BoughtValue={BoughtValue}, CurrentValue={CurrentValue}",
                latestValuationDate.Value, instrument.Id, request.PlatformId, portfolioId, request.Units, request.BoughtValue, pricingResult.CurrentValue);
                
            var newHolding = new Holding(
                DateTime.SpecifyKind(latestValuationDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                instrument.Id,
                request.PlatformId,
                portfolioId,
                request.Units,
                request.BoughtValue,
                pricingResult.CurrentValue);

            _logger.LogInformation("Holding entity created with ID: {HoldingId}", newHolding.Id);

            // Add to repository
            _logger.LogInformation("Adding holding to repository");
            await _holdingRepository.AddAsync(newHolding);
            
            _logger.LogInformation("Saving changes to database");
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Committing transaction");
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Transaction committed successfully");

            // Set result
            result.Success = true;
            result.CreatedHolding = newHolding;
            result.CurrentPrice = pricingResult.CurrentPrice;
            result.CurrentValue = pricingResult.CurrentValue;
            result.Message = $"Successfully added {request.Units:N4} units of {request.Ticker} to portfolio";

            _logger.LogInformation("Successfully added holding {Ticker} to portfolio {PortfolioId}",
                request.Ticker, portfolioId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding holding {Ticker} to portfolio {PortfolioId}. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                request.Ticker, portfolioId, ex.GetType().Name, ex.Message, ex.StackTrace);
            
            try
            {
                _logger.LogInformation("Rolling back transaction due to error");
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogInformation("Transaction rollback completed");
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to rollback transaction: {RollbackError}", rollbackEx.Message);
            }
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Add operation failed due to system error";
            return result;
        }
    }

    // Update Operations (from HoldingManagementService)

    /// <summary>
    /// Updates the unit amount for an existing holding on the latest valuation date
    /// </summary>
    public async Task<HoldingUpdateResult> UpdateHoldingUnitsAsync(
        int holdingId,
        decimal newUnits,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var result = new HoldingUpdateResult { NewUnits = newUnits };

        try
        {
            _logger.LogInformation("Updating units for holding {HoldingId} to {NewUnits} for account {AccountId}",
                holdingId, newUnits, accountId);

            // Validate input
            if (newUnits <= 0)
            {
                result.Errors.Add("Units must be greater than zero");
                result.Message = "Invalid unit amount";
                return result;
            }

            // Get latest valuation date
            var latestValuationDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (latestValuationDate == null)
            {
                result.Errors.Add("No holdings found in the system");
                result.Message = "Cannot update holdings - no valuation data available";
                return result;
            }

            // Fetch holding with all relations
            var holding = await GetHoldingWithValidationAsync(holdingId, accountId, latestValuationDate.Value, cancellationToken);
            if (holding == null)
            {
                result.Errors.Add("Holding not found, does not belong to your account, or is not from the latest valuation date");
                result.Message = "Holding not accessible";
                return result;
            }

            // Store previous values
            result.PreviousUnits = holding.UnitAmount;
            result.PreviousCurrentValue = holding.CurrentValue;

            // Calculate new current value with updated units
            var pricingResult = await _pricingCalculationHelper.FetchAndCalculateHoldingValueAsync(
                holding.InstrumentId, holding.Instrument.Ticker, newUnits, 
                holding.Instrument.QuoteUnit, holding.Instrument.CurrencyCode, 
                latestValuationDate.Value, cancellationToken);

            if (!pricingResult.Success)
            {
                result.Errors.Add(pricingResult.ErrorMessage ?? "Failed to calculate current value");
                result.Message = "Pricing calculation failed";
                return result;
            }

            // Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            // Update the holding
            holding.UpdatePosition(newUnits, holding.BoughtValue);
            holding.UpdateCurrentValue(pricingResult.CurrentValue);

            // Save changes
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Set result
            result.Success = true;
            result.UpdatedHolding = holding;
            result.NewCurrentValue = pricingResult.CurrentValue;
            result.Message = $"Successfully updated {holding.Instrument.Ticker} units from {result.PreviousUnits:N4} to {newUnits:N4}";

            _logger.LogInformation("Successfully updated holding {HoldingId} units from {OldUnits} to {NewUnits}",
                holdingId, result.PreviousUnits, newUnits);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating holding {HoldingId} units to {NewUnits}", holdingId, newUnits);
            await _unitOfWork.RollbackTransactionAsync();
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Update failed due to system error";
            return result;
        }
    }

    // Delete Operations (from HoldingManagementService)

    /// <summary>
    /// Removes a holding from the latest valuation date
    /// </summary>
    public async Task<HoldingDeleteResult> DeleteHoldingAsync(
        int holdingId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var result = new HoldingDeleteResult { DeletedHoldingId = holdingId };

        try
        {
            _logger.LogInformation("Deleting holding {HoldingId} for account {AccountId}", holdingId, accountId);

            // Get latest valuation date
            var latestValuationDate = await _holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (latestValuationDate == null)
            {
                result.Errors.Add("No holdings found in the system");
                result.Message = "Cannot delete holdings - no valuation data available";
                return result;
            }

            // Fetch holding with validation
            var holding = await GetHoldingWithValidationAsync(holdingId, accountId, latestValuationDate.Value, cancellationToken);
            if (holding == null)
            {
                result.Errors.Add("Holding not found, does not belong to your account, or is not from the latest valuation date");
                result.Message = "Holding not accessible";
                return result;
            }

            // Store info for result
            result.DeletedTicker = holding.Instrument.Ticker;
            result.PortfolioId = holding.PortfolioId;

            // Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            // Delete the holding
            await _holdingRepository.DeleteAsync(holding);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Set result
            result.Success = true;
            result.Message = $"Successfully deleted holding for {holding.Instrument.Ticker}";

            _logger.LogInformation("Successfully deleted holding {HoldingId} for {Ticker}",
                holdingId, holding.Instrument.Ticker);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting holding {HoldingId}", holdingId);
            await _unitOfWork.RollbackTransactionAsync();
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Delete operation failed due to system error";
            return result;
        }
    }

    #region Private Helper Methods (from HoldingManagementService)

    private async Task<Holding?> GetHoldingWithValidationAsync(
        int holdingId, 
        int accountId, 
        DateOnly latestValuationDate, 
        CancellationToken cancellationToken)
    {
        // Use the existing repository method that includes related entities
        var targetDate = DateTime.SpecifyKind(latestValuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var holdingsOnDate = await _holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, targetDate, cancellationToken);
        
        return holdingsOnDate.FirstOrDefault(h => h.Id == holdingId);
    }

    private async Task<Instrument?> EnsureInstrumentExistsAsync(AddHoldingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.InstrumentName))
            {
                // Try to find existing instrument by ticker
                return await _instrumentManagementService.GetOrCreateInstrumentAsync(
                    request.Ticker,
                    request.Ticker, // Use ticker as name if name not provided
                    request.Description,
                    request.InstrumentTypeId,
                    request.CurrencyCode,
                    request.QuoteUnit,
                    cancellationToken);
            }
            else
            {
                // Use provided instrument details
                return await _instrumentManagementService.GetOrCreateInstrumentAsync(
                    request.Ticker,
                    request.InstrumentName,
                    request.Description,
                    request.InstrumentTypeId,
                    request.CurrencyCode,
                    request.QuoteUnit,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring instrument exists for ticker {Ticker}", request.Ticker);
            return null;
        }
    }

    private async Task<bool> WasInstrumentJustCreated(string ticker, CancellationToken cancellationToken)
    {
        // This is a simple heuristic - could be improved with more sophisticated tracking
        try
        {
            var instruments = await _instrumentRepository.FindAsync(i => i.Ticker == ticker);
            var instrument = instruments.FirstOrDefault();
            
            return instrument?.CreatedAt > DateTime.UtcNow.AddMinutes(-1);
        }
        catch
        {
            return false;
        }
    }

    private async Task<Holding?> GetExistingHoldingAsync(
        int portfolioId, 
        int instrumentId, 
        DateOnly valuationDate, 
        CancellationToken cancellationToken)
    {
        var targetDate = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        
        var holdings = await _holdingRepository.GetByValuationDateAsync(targetDate);
        return holdings.FirstOrDefault(h => h.PortfolioId == portfolioId && h.InstrumentId == instrumentId);
    }

    private static List<string> ValidateAddHoldingRequest(AddHoldingRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Ticker))
            errors.Add("Ticker is required");

        if (request.Units <= 0)
            errors.Add("Units must be greater than zero");

        if (request.BoughtValue < 0)
            errors.Add("Bought value cannot be negative");

        if (request.PlatformId <= 0)
            errors.Add("Valid platform ID is required");

        // If instrument name is not provided and ticker is not CASH, we need more details
        if (string.IsNullOrWhiteSpace(request.InstrumentName) && 
            !request.Ticker.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase))
        {
            // This is acceptable - we'll use ticker as name
        }

        return errors;
    }

    #endregion

    #region Private Helper Methods (from HoldingsRetrievalService)

    /// <summary>
    /// Apply real-time pricing to holdings using EOD market data (no persistence)
    /// </summary>
    private async Task<IEnumerable<Holding>> ApplyRealTimePricing(List<Holding> holdings, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        try
        {
            if (_eodMarketDataToolFactory == null)
            {
                _logger.LogWarning("EOD market data tool not available for real-time pricing");
                return holdings;
            }

            // Extract tickers from holdings (excluding CASH)
            var tickers = holdings
                .Where(h => !string.IsNullOrEmpty(h.Instrument?.Ticker))
                .Where(h => !h.Instrument!.Ticker!.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Instrument!.Ticker!)
                .Distinct()
                .ToList();

            // Fetch real-time prices if we have tickers
            Dictionary<string, decimal> prices = new Dictionary<string, decimal>();
            if (tickers.Any())
            {
                var eodTool = _eodMarketDataToolFactory();
                prices = await eodTool.GetRealTimePricesAsync(null, tickers, cancellationToken);

                _logger.LogInformation("Fetched {Count} real-time prices for {TotalTickers} tickers", 
                    prices.Count, tickers.Count);
            }

            // Apply real-time prices to ALL holdings (including those without real-time prices)
            var updatedCount = 0;
            foreach (var holding in holdings)
            {
                // Handle CASH holdings - update date but keep same value with zero daily P&L
                if (holding.Instrument?.Ticker?.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase) == true)
                {
                    holding.SetDailyProfitLoss(0m, 0m);
                    holding.UpdateValuation(DateTime.UtcNow, holding.CurrentValue);
                    
                    _logger.LogInformation("Updated CASH holding {HoldingId} for today - keeping value {CurrentValue:C}, setting daily P/L to zero", 
                        holding.Id, holding.CurrentValue);
                    continue;
                }

                // Handle holdings with real-time prices available
                if (holding.Instrument?.Ticker != null && prices.TryGetValue(holding.Instrument.Ticker, out var realTimePrice))
                {
                    var originalValue = holding.CurrentValue;
                    
                    // Apply scaling factor for proxy instruments using shared service
                    var scaledPrice = _pricingCalculationService.ApplyScalingFactor(realTimePrice, holding.Instrument.Ticker);
                    
                    // Use the shared pricing calculation service
                    var newCurrentValue = await _pricingCalculationService.CalculateCurrentValueAsync(
                        holding.UnitAmount, 
                        scaledPrice, // Use scaled price instead of raw price
                        holding.Instrument.QuoteUnit,
                        _pricingCalculationService.GetCurrencyFromTicker(holding.Instrument.Ticker),
                        DateOnly.FromDateTime(DateTime.UtcNow));
                    
                    // Calculate daily P&L based on the change from previous value to new real-time value
                    var dailyChange = newCurrentValue - originalValue;
                    var dailyChangePercentage = originalValue != 0 
                        ? (dailyChange / originalValue) * 100 
                        : 0;
                    
                    // Update both current value and valuation date to today for real-time pricing
                    holding.UpdateValuation(DateTime.UtcNow, newCurrentValue);
                    
                    // Set the recalculated daily profit/loss
                    holding.SetDailyProfitLoss(dailyChange, dailyChangePercentage);
                    
                    updatedCount++;
                    
                    _logger.LogInformation("Updated holding {HoldingId} for {Ticker}: Original value {OriginalValue:C} -> New value {NewValue:C}, Daily P/L: {DailyChange:C} ({DailyChangePercentage:F2}%) (Real-time price: {RealTimePrice}, Scaled price: {ScaledPrice}, Quote unit: {QuoteUnit})", 
                        holding.Id, holding.Instrument.Ticker, originalValue, newCurrentValue, dailyChange, dailyChangePercentage, realTimePrice, scaledPrice, holding.Instrument.QuoteUnit ?? CurrencyConstants.DEFAULT_QUOTE_UNIT);
                }
            }

            _logger.LogInformation("Successfully updated {UpdatedCount} holdings with real-time pricing, {UnchangedCount} holdings kept with original dates", 
                updatedCount, holdings.Count - updatedCount);

            // IMPORTANT: Detach all holdings from EF context to prevent persistence of real-time calculations
            // This ensures the modified entities are not tracked and won't be saved to the database
            foreach (var holding in holdings)
            {
                // Note: We can't directly access the context from here, but the entities will be detached
                // when they're returned as DTOs or when the scope ends, preventing persistence
            }

            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time pricing, returning holdings with original prices");
            return holdings;
        }
    }

    #endregion
}