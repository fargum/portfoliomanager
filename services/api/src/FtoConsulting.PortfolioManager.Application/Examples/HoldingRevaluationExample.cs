using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Examples;

/// <summary>
/// Example demonstrating how to use the HoldingRevaluationService to revalue portfolio holdings
/// based on current market prices, including quote unit conversions (GBP vs GBX)
/// </summary>
public static class HoldingRevaluationExample
{
    /// <summary>
    /// Demonstrates the holding revaluation process for a specific valuation date
    /// </summary>
    /// <param name="revaluationService">The holding revaluation service</param>
    /// <param name="logger">Logger for tracking the revaluation process</param>
    public static async Task<bool> PerformHoldingRevaluationExample(
        IHoldingRevaluationService revaluationService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("=== Holding Revaluation Example ===");
            
            // Set the target valuation date (typically today or a specific business date)
            var valuationDate = DateOnly.FromDateTime(DateTime.Today);
            
            logger.LogInformation("Starting holding revaluation for {ValuationDate}", valuationDate);
            
            // Perform the revaluation
            var result = await revaluationService.RevalueHoldingsAsync(valuationDate);
            
            // Display results
            logger.LogInformation("Revaluation Results:");
            logger.LogInformation("- Valuation Date: {ValuationDate}", result.ValuationDate);
            logger.LogInformation("- Source Date: {SourceDate}", result.SourceValuationDate);
            logger.LogInformation("- Total Holdings Processed: {Total}", result.TotalHoldings);
            logger.LogInformation("- Successfully Revalued: {Success}", result.SuccessfulRevaluations);
            logger.LogInformation("- Failed Revaluations: {Failed}", result.FailedRevaluations);
            logger.LogInformation("- Replaced Existing Holdings: {Replaced}", result.ReplacedHoldings);
            logger.LogInformation("- Duration: {Duration}ms", result.Duration.TotalMilliseconds);
            
            // Report any failures
            if (result.FailedInstruments.Any())
            {
                logger.LogWarning("Failed to revalue {Count} instruments:", result.FailedInstruments.Count);
                foreach (var failed in result.FailedInstruments)
                {
                    logger.LogWarning("- {ISIN} ({Name}): {Error} [{ErrorCode}]", 
                        failed.ISIN, failed.InstrumentName, failed.ErrorMessage, failed.ErrorCode);
                }
            }
            
            var successRate = result.TotalHoldings > 0 
                ? (double)result.SuccessfulRevaluations / result.TotalHoldings * 100 
                : 0;
            
            logger.LogInformation("Revaluation Success Rate: {SuccessRate:F1}%", successRate);
            
            return result.SuccessfulRevaluations > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during holding revaluation example");
            return false;
        }
    }

    /// <summary>
    /// Demonstrates revaluation for multiple consecutive business days
    /// </summary>
    /// <param name="revaluationService">The holding revaluation service</param>
    /// <param name="logger">Logger for tracking the revaluation process</param>
    /// <param name="days">Number of days to revalue (default: 5)</param>
    public static async Task<Dictionary<DateOnly, bool>> PerformMultiDayRevaluationExample(
        IHoldingRevaluationService revaluationService,
        ILogger logger,
        int days = 5)
    {
        var results = new Dictionary<DateOnly, bool>();
        
        logger.LogInformation("=== Multi-Day Holding Revaluation Example ===");
        
        // Start from today and go backwards to ensure we have price data
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        
        for (int i = 0; i < days; i++)
        {
            var targetDate = startDate.AddDays(-i);
            
            try
            {
                logger.LogInformation("Revaluing holdings for {Date} (Day {Current} of {Total})", 
                    targetDate, i + 1, days);
                
                var result = await revaluationService.RevalueHoldingsAsync(targetDate);
                
                var success = result.SuccessfulRevaluations > 0;
                results[targetDate] = success;
                
                logger.LogInformation("Date {Date}: {Status} ({Success}/{Total} successful)", 
                    targetDate, 
                    success ? "✓ SUCCESS" : "✗ FAILED", 
                    result.SuccessfulRevaluations, 
                    result.TotalHoldings);
                    
                // Small delay between requests to be gentle on the system
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to revalue holdings for {Date}", targetDate);
                results[targetDate] = false;
            }
        }
        
        var successfulDays = results.Values.Count(x => x);
        logger.LogInformation("Multi-day revaluation completed: {Success}/{Total} days successful", 
            successfulDays, days);
        
        return results;
    }

    /// <summary>
    /// Example showing how quote unit conversion affects calculations
    /// </summary>
    public static void ExplainQuoteUnitConversions(ILogger logger)
    {
        logger.LogInformation("=== Quote Unit Conversion Examples ===");
        
        // Example 1: GBP instrument (no conversion needed)
        decimal quantity1 = 100m;
        decimal priceGBP = 150.25m;
        decimal valueGBP = quantity1 * priceGBP;
        
        logger.LogInformation("GBP Example:");
        logger.LogInformation("- Quantity: {Quantity} shares", quantity1);
        logger.LogInformation("- Price: £{Price} (GBP)", priceGBP);
        logger.LogInformation("- Current Value: £{Value}", valueGBP);
        
        // Example 2: GBX instrument (conversion from pence to pounds)
        decimal quantity2 = 100m;
        decimal priceGBX = 15025m; // Same £150.25 but in pence
        decimal adjustedPrice = priceGBX / 100m; // Convert pence to pounds
        decimal valueConverted = quantity2 * adjustedPrice;
        
        logger.LogInformation("GBX Example:");
        logger.LogInformation("- Quantity: {Quantity} shares", quantity2);
        logger.LogInformation("- Price: {Price}p (GBX)", priceGBX);
        logger.LogInformation("- Adjusted Price: £{AdjustedPrice} (converted to GBP)", adjustedPrice);
        logger.LogInformation("- Current Value: £{Value}", valueConverted);
        
        logger.LogInformation("Both examples result in the same £{Value} current value", valueGBP);
    }
}