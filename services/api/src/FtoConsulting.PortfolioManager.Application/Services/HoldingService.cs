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
public class HoldingService(
    IHoldingRepository holdingRepository,
    IPortfolioRepository portfolioRepository,
    IInstrumentRepository instrumentRepository,
    IInstrumentManagementService instrumentManagementService,
    IPricingCalculationHelper pricingCalculationHelper,
    IPricingCalculationService pricingCalculationService,
    IUnitOfWork unitOfWork,
    ILogger<HoldingService> logger,
    Func<EodMarketDataTool>? eodMarketDataToolFactory = null) : IHoldingService
{

    /// <summary>
    /// Retrieves all holdings for a given account on a specific date
    /// Supports both historical data retrieval and real-time pricing for current date
    /// </summary>
    public async Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            IEnumerable<Holding> holdings;
            var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            
            if (valuationDate < today)
            {
                // Historical data - get holdings for the specific date
                holdings = await holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
                logger.LogInformation("Retrieved {Count} historical holdings for account {AccountId} on date {ValuationDate}", 
                    holdings.Count(), accountId, valuationDate);
                return holdings;
            }

            if (valuationDate > today)
            {
                // Future date - not supported, return empty
                logger.LogWarning("Future date {ValuationDate} requested, returning empty holdings", valuationDate);
                holdings = Enumerable.Empty<Holding>();
                return holdings;
            }

            holdings = await holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, dateTime, cancellationToken);
            if (holdings.Any())
            {
                logger.LogInformation("Retrieved {Count} holdings for account {AccountId} on today's date {ValuationDate}", 
                    holdings.Count(), accountId, valuationDate);
                return holdings;
            }

            // Real-time data - get the most recent holdings and apply real-time pricing
            var latestDate = await holdingRepository.GetLatestValuationDateAsync(cancellationToken);
            if (latestDate.HasValue)
            {
                // Get ALL holdings from the latest date, then filter by account (using no-tracking for real-time pricing)
                var allLatestHoldings = await holdingRepository.GetHoldingsByValuationDateWithInstrumentsNoTrackingAsync(latestDate.Value, cancellationToken);
                holdings = allLatestHoldings.Where(h => h.Portfolio.AccountId == accountId).ToList();
                logger.LogInformation("Retrieved {Count} latest holdings for account {AccountId} from date {LatestDate} for real-time pricing (no tracking to prevent persistence)", 
                    holdings.Count(), accountId, latestDate.Value);

                if (eodMarketDataToolFactory != null)
                {
                    logger.LogInformation("Applying real-time pricing for today's date: {Date}", valuationDate);
                    holdings = await ApplyRealTimePricing(holdings.ToList(), valuationDate, cancellationToken);
                }
                else
                {
                    logger.LogWarning("EOD market data tool not available for real-time pricing, returning latest holdings with historical prices");
                }
            }
            else
            {
                logger.LogWarning("No historical holdings found for account {AccountId}, returning empty holdings", accountId);
                holdings = Enumerable.Empty<Holding>();
            }
                      
            return holdings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving holdings for account {AccountId} on date {ValuationDate}", accountId, valuationDate);
            throw;
        }
    }

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
            logger.LogInformation("Adding new holding {Ticker} to portfolio {PortfolioId} for account {AccountId}",
                request.Ticker, portfolioId, accountId);

            // Validate and setup prerequisites
            if (!await ValidateAndSetupAddHoldingAsync(result, request, portfolioId, accountId, cancellationToken))
                return result;

            // Get latest valuation date (validated in previous step)
            var latestValuationDate = await holdingRepository.GetLatestValuationDateAsync(cancellationToken);

            // Process instrument and check for duplicates
            var instrument = await ProcessInstrumentForAddAsync(result, request, portfolioId, latestValuationDate!.Value, cancellationToken);
            if (instrument == null)
                return result;

            // Calculate pricing and create holding
            var holding = await CreateAndSaveHoldingAsync(result, request, portfolioId, instrument, latestValuationDate.Value, cancellationToken);
            if (holding == null)
                return result;

            // Success
            result.Success = true;
            result.CreatedHolding = holding;
            result.Message = $"Successfully added {request.Units:N4} units of {request.Ticker} to portfolio";

            logger.LogInformation("Successfully added holding {Ticker} to portfolio {PortfolioId}",
                request.Ticker, portfolioId);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding holding {Ticker} to portfolio {PortfolioId}", 
                request.Ticker, portfolioId);
            
            await RollbackTransactionSafely(ex);
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Add operation failed due to system error";
            return result;
        }
    }

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
            logger.LogInformation("Updating units for holding {HoldingId} to {NewUnits} for account {AccountId}",
                holdingId, newUnits, accountId);

            // Validate input
            if (newUnits <= 0)
            {
                result.Errors.Add("Units must be greater than zero");
                result.Message = "Invalid unit amount";
                return result;
            }

            // Get latest valuation date
            var latestValuationDate = await holdingRepository.GetLatestValuationDateAsync(cancellationToken);
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
            var pricingResult = await pricingCalculationHelper.FetchAndCalculateHoldingValueAsync(
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
            await unitOfWork.BeginTransactionAsync();

            // Update the holding
            holding.UpdatePosition(newUnits, holding.BoughtValue);
            holding.UpdateCurrentValue(pricingResult.CurrentValue);

            // Save changes
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();

            // Set result
            result.Success = true;
            result.UpdatedHolding = holding;
            result.NewCurrentValue = pricingResult.CurrentValue;
            result.Message = $"Successfully updated {holding.Instrument.Ticker} units from {result.PreviousUnits:N4} to {newUnits:N4}";

            logger.LogInformation("Successfully updated holding {HoldingId} units from {OldUnits} to {NewUnits}",
                holdingId, result.PreviousUnits, newUnits);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating holding {HoldingId} units to {NewUnits}", holdingId, newUnits);
            await unitOfWork.RollbackTransactionAsync();
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Update failed due to system error";
            return result;
        }
    }

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
            logger.LogInformation("Deleting holding {HoldingId} for account {AccountId}", holdingId, accountId);

            // Get latest valuation date
            var latestValuationDate = await holdingRepository.GetLatestValuationDateAsync(cancellationToken);
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
            await unitOfWork.BeginTransactionAsync();

            // Delete the holding
            await holdingRepository.DeleteAsync(holding);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();

            // Set result
            result.Success = true;
            result.Message = $"Successfully deleted holding for {holding.Instrument.Ticker}";

            logger.LogInformation("Successfully deleted holding {HoldingId} for {Ticker}",
                holdingId, holding.Instrument.Ticker);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting holding {HoldingId}", holdingId);
            await unitOfWork.RollbackTransactionAsync();
            
            result.Errors.Add($"An error occurred: {ex.Message}");
            result.Message = "Delete operation failed due to system error";
            return result;
        }
    }

    #region Private Helper Methods (from HoldingManagementService)

    /// <summary>
    /// Validates request and sets up prerequisites for adding a holding
    /// </summary>
    private async Task<bool> ValidateAndSetupAddHoldingAsync(
        HoldingAddResult result,
        AddHoldingRequest request,
        int portfolioId,
        int accountId,
        CancellationToken cancellationToken)
    {
        // Validate input
        var validationErrors = ValidateAddHoldingRequest(request);
        if (validationErrors.Any())
        {
            result.Errors.AddRange(validationErrors);
            result.Message = "Validation failed";
            return false;
        }

        // Get latest valuation date
        var latestValuationDate = await holdingRepository.GetLatestValuationDateAsync(cancellationToken);
        if (latestValuationDate == null)
        {
            result.Errors.Add("No holdings found in the system");
            result.Message = "Cannot add holdings - no valuation data available";
            return false;
        }

        // Verify portfolio ownership
        var portfolio = await portfolioRepository.GetByIdAsync(portfolioId);
        if (portfolio == null || portfolio.AccountId != accountId)
        {
            logger.LogWarning("Portfolio access denied: PortfolioId={PortfolioId}, AccountId={AccountId}",
                portfolioId, accountId);
            result.Errors.Add("Portfolio not found or does not belong to your account");
            result.Message = "Portfolio not accessible";
            return false;
        }

        logger.LogInformation("Portfolio verification successful: PortfolioId={PortfolioId}, PortfolioName={PortfolioName}",
            portfolio.Id, portfolio.Name);

        // Validate dependency injection
        if (unitOfWork == null)
        {
            logger.LogError("UnitOfWork is null - dependency injection failure");
            result.Errors.Add("Internal service configuration error - UnitOfWork not available");
            result.Message = "Service configuration error";
            return false;
        }

        // Begin transaction
        try
        {
            await unitOfWork.BeginTransactionAsync();
            logger.LogInformation("Database transaction started successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to begin database transaction: {ErrorMessage}", ex.Message);
            result.Errors.Add($"Database transaction error: {ex.Message}");
            result.Message = "Failed to begin database transaction";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Processes instrument creation/retrieval and checks for duplicate holdings
    /// </summary>
    private async Task<Instrument?> ProcessInstrumentForAddAsync(
        HoldingAddResult result,
        AddHoldingRequest request,
        int portfolioId,
        DateOnly latestValuationDate,
        CancellationToken cancellationToken)
    {
        // Ensure instrument exists
        logger.LogInformation("Starting instrument processing for ticker: {Ticker}", request.Ticker);
        var instrument = await EnsureInstrumentExistsAsync(request, cancellationToken);
        if (instrument == null)
        {
            result.Errors.Add("Failed to create or retrieve instrument");
            result.Message = "Instrument processing failed";
            return null;
        }

        result.Instrument = instrument;
        result.InstrumentCreated = await WasInstrumentJustCreated(request.Ticker, cancellationToken);

        logger.LogInformation("Instrument processed: {InstrumentId}, Created: {InstrumentCreated}",
            instrument.Id, result.InstrumentCreated);

        // Check for duplicate holding
        logger.LogInformation("Checking for duplicate holding: Portfolio={PortfolioId}, Instrument={InstrumentId}, Date={ValuationDate}",
            portfolioId, instrument.Id, latestValuationDate);

        var existingHolding = await GetExistingHoldingAsync(portfolioId, instrument.Id, latestValuationDate, cancellationToken);
        if (existingHolding != null)
        {
            logger.LogWarning("Duplicate holding found: {ExistingHoldingId}", existingHolding.Id);
            result.Errors.Add($"A holding for {request.Ticker} already exists in this portfolio for the current valuation date");
            result.Message = "Duplicate holding detected";
            return null;
        }

        logger.LogInformation("No duplicate holding found, proceeding with pricing calculation");
        return instrument;
    }

    /// <summary>
    /// Creates the holding entity with pricing and saves to database
    /// </summary>
    private async Task<Holding?> CreateAndSaveHoldingAsync(
        HoldingAddResult result,
        AddHoldingRequest request,
        int portfolioId,
        Instrument instrument,
        DateOnly latestValuationDate,
        CancellationToken cancellationToken)
    {
        // Calculate current value
        logger.LogInformation("Starting pricing calculation for {Ticker}, Units={Units}",
            instrument.Ticker, request.Units);

        var pricingResult = await pricingCalculationHelper.FetchAndCalculateHoldingValueAsync(
            instrument.Id, instrument.Ticker, request.Units,
            instrument.QuoteUnit, instrument.CurrencyCode,
            latestValuationDate, cancellationToken);

        if (!pricingResult.Success)
        {
            logger.LogError("Pricing calculation failed: {ErrorMessage}", pricingResult.ErrorMessage);
            result.Errors.Add(pricingResult.ErrorMessage ?? "Failed to calculate current value");
            result.Message = "Pricing calculation failed";
            return null;
        }

        logger.LogInformation("Pricing calculation successful: CurrentPrice={CurrentPrice}, CurrentValue={CurrentValue}",
            pricingResult.CurrentPrice, pricingResult.CurrentValue);

        // Create new holding
        var newHolding = new Holding(
            DateTime.SpecifyKind(latestValuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            instrument.Id,
            request.PlatformId,
            portfolioId,
            request.Units,
            request.BoughtValue,
            pricingResult.CurrentValue);

        logger.LogInformation("Holding entity created with ID: {HoldingId}", newHolding.Id);

        // Save to database
        await holdingRepository.AddAsync(newHolding);
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.CommitTransactionAsync();

        logger.LogInformation("Transaction committed successfully");

        // Set pricing result data
        result.CurrentPrice = pricingResult.CurrentPrice;
        result.CurrentValue = pricingResult.CurrentValue;

        return newHolding;
    }

    /// <summary>
    /// Safely rolls back transaction and logs any errors
    /// </summary>
    private async Task RollbackTransactionSafely(Exception originalException)
    {
        try
        {
            logger.LogInformation("Rolling back transaction due to error");
            await unitOfWork.RollbackTransactionAsync();
            logger.LogInformation("Transaction rollback completed");
        }
        catch (Exception rollbackEx)
        {
            logger.LogError(rollbackEx, "Failed to rollback transaction: {RollbackError}", rollbackEx.Message);
        }
    }

    private async Task<Holding?> GetHoldingWithValidationAsync(
        int holdingId, 
        int accountId, 
        DateOnly latestValuationDate, 
        CancellationToken cancellationToken)
    {
        // Use the existing repository method that includes related entities
        var targetDate = DateTime.SpecifyKind(latestValuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var holdingsOnDate = await holdingRepository.GetHoldingsByAccountAndDateAsync(accountId, targetDate, cancellationToken);
        
        return holdingsOnDate.FirstOrDefault(h => h.Id == holdingId);
    }

    private async Task<Instrument?> EnsureInstrumentExistsAsync(AddHoldingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.InstrumentName))
            {
                // Try to find existing instrument by ticker
                return await instrumentManagementService.GetOrCreateInstrumentAsync(
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
                return await instrumentManagementService.GetOrCreateInstrumentAsync(
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
            logger.LogError(ex, "Error ensuring instrument exists for ticker {Ticker}", request.Ticker);
            return null;
        }
    }

    private async Task<bool> WasInstrumentJustCreated(string ticker, CancellationToken cancellationToken)
    {
        // This is a simple heuristic - could be improved with more sophisticated tracking
        try
        {
            var instruments = await instrumentRepository.FindAsync(i => i.Ticker == ticker);
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
        
        var holdings = await holdingRepository.GetByValuationDateAsync(targetDate);
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
            if (eodMarketDataToolFactory == null)
            {
                logger.LogWarning("EOD market data tool not available for real-time pricing");
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
                var eodTool = eodMarketDataToolFactory();
                prices = await eodTool.GetRealTimePricesAsync(null, tickers, cancellationToken);

                logger.LogInformation("Fetched {Count} real-time prices for {TotalTickers} tickers", 
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
                    
                    logger.LogInformation("Updated CASH holding {HoldingId} for today - keeping value {CurrentValue:C}, setting daily P/L to zero", 
                        holding.Id, holding.CurrentValue);
                    continue;
                }

                // Handle holdings with real-time prices available
                if (holding.Instrument?.Ticker != null && prices.TryGetValue(holding.Instrument.Ticker, out var realTimePrice))
                {
                    var originalValue = holding.CurrentValue;
                    
                    // Apply scaling factor for proxy instruments using shared service
                    var scaledPrice = pricingCalculationService.ApplyScalingFactor(realTimePrice, holding.Instrument.Ticker);
                    
                    // Use the shared pricing calculation service
                    var newCurrentValue = await pricingCalculationService.CalculateCurrentValueAsync(
                        holding.UnitAmount, 
                        scaledPrice, // Use scaled price instead of raw price
                        holding.Instrument.QuoteUnit,
                        pricingCalculationService.GetCurrencyFromTicker(holding.Instrument.Ticker),
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
                    
                    logger.LogInformation("Updated holding {HoldingId} for {Ticker}: Original value {OriginalValue:C} -> New value {NewValue:C}, Daily P/L: {DailyChange:C} ({DailyChangePercentage:F2}%) (Real-time price: {RealTimePrice}, Scaled price: {ScaledPrice}, Quote unit: {QuoteUnit})", 
                        holding.Id, holding.Instrument.Ticker, originalValue, newCurrentValue, dailyChange, dailyChangePercentage, realTimePrice, scaledPrice, holding.Instrument.QuoteUnit ?? CurrencyConstants.DEFAULT_QUOTE_UNIT);
                }
            }

            logger.LogInformation("Successfully updated {UpdatedCount} holdings with real-time pricing, {UnchangedCount} holdings kept with original dates", 
                updatedCount, holdings.Count - updatedCount);


            return holdings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying real-time pricing, returning holdings with original prices");
            return holdings;
        }
    }

    #endregion
}