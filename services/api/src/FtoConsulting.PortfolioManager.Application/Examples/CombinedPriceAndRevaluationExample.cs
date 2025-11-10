using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Examples;

/// <summary>
/// Example demonstrating how to use the combined price fetch and revaluation operation
/// </summary>
public static class CombinedPriceAndRevaluationExample
{
    /// <summary>
    /// Demonstrates the combined price fetch and holding revaluation process for a specific valuation date
    /// </summary>
    /// <param name="holdingRevaluationService">The holding revaluation service</param>
    /// <param name="logger">Logger for tracking the operation process</param>
    public static async Task<bool> PerformCombinedOperationExample(
        IHoldingRevaluationService holdingRevaluationService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("=== Combined Price Fetch and Revaluation Example ===");
            
            // Use a recent business day for the example
            var valuationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            
            logger.LogInformation("Starting combined operation for valuation date: {ValuationDate}", valuationDate);
            
            // Perform the combined operation
            var result = await holdingRevaluationService.FetchPricesAndRevalueHoldingsAsync(valuationDate);
            
            // Display the results
            logger.LogInformation("=== Combined Operation Results ===");
            logger.LogInformation("Valuation Date: {ValuationDate}", result.ValuationDate);
            logger.LogInformation("Overall Success: {OverallSuccess}", result.OverallSuccess);
            logger.LogInformation("Total Duration: {TotalDuration}ms", result.TotalDuration.TotalMilliseconds);
            logger.LogInformation("Summary: {Summary}", result.Summary);
            
            // Price Fetch Results
            logger.LogInformation("=== Price Fetch Results ===");
            logger.LogInformation("- Total Tickers: {TotalTickers}", result.PriceFetchResult.TotalTickers);
            logger.LogInformation("- Successful Prices: {SuccessfulPrices}", result.PriceFetchResult.SuccessfulPrices);
            logger.LogInformation("- Failed Prices: {FailedPrices}", result.PriceFetchResult.FailedPrices);
            logger.LogInformation("- Fetch Duration: {FetchDuration}ms", result.PriceFetchResult.FetchDuration.TotalMilliseconds);
            
            // Revaluation Results
            logger.LogInformation("=== Revaluation Results ===");
            logger.LogInformation("- Total Holdings: {TotalHoldings}", result.HoldingRevaluationResult.TotalHoldings);
            logger.LogInformation("- Successful Revaluations: {SuccessfulRevaluations}", result.HoldingRevaluationResult.SuccessfulRevaluations);
            logger.LogInformation("- Failed Revaluations: {FailedRevaluations}", result.HoldingRevaluationResult.FailedRevaluations);
            logger.LogInformation("- Revaluation Duration: {RevaluationDuration}ms", result.HoldingRevaluationResult.Duration.TotalMilliseconds);
            
            // Report any price fetch failures
            if (result.PriceFetchResult.FailedTickers.Any())
            {
                logger.LogWarning("Failed to fetch prices for {Count} tickers:", result.PriceFetchResult.FailedTickers.Count);
                foreach (var failed in result.PriceFetchResult.FailedTickers)
                {
                    logger.LogWarning("- {Ticker}: {Error} [{ErrorCode}]", 
                        failed.Ticker, failed.ErrorMessage, failed.ErrorCode);
                }
            }
            
            // Report any revaluation failures
            if (result.HoldingRevaluationResult.FailedInstruments.Any())
            {
                logger.LogWarning("Failed to revalue {Count} instruments:", result.HoldingRevaluationResult.FailedInstruments.Count);
                foreach (var failed in result.HoldingRevaluationResult.FailedInstruments)
                {
                    logger.LogWarning("- {Ticker} ({Name}): {Error} [{ErrorCode}]", 
                        failed.Ticker, failed.InstrumentName, failed.ErrorMessage, failed.ErrorCode);
                }
            }
            
            logger.LogInformation("=== Combined Operation Example Completed ===");
            return result.OverallSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during combined operation example");
            return false;
        }
    }

    /// <summary>
    /// Demonstrates the benefit of the combined operation vs separate operations
    /// </summary>
    public static void ExplainCombinedOperationBenefits(ILogger logger)
    {
        logger.LogInformation("=== Combined Operation Benefits ===");
        logger.LogInformation("1. **Convenience**: Single API call performs both price fetch and revaluation");
        logger.LogInformation("2. **Consistency**: Ensures prices are fetched before revaluation attempt");
        logger.LogInformation("3. **Comprehensive Results**: Returns statistics for both operations");
        logger.LogInformation("4. **Error Handling**: Coordinated error handling across both operations");
        logger.LogInformation("5. **Timing**: Accurate total duration measurement for the complete workflow");
        logger.LogInformation("");
        logger.LogInformation("Use Cases:");
        logger.LogInformation("- Daily portfolio revaluation workflows");
        logger.LogInformation("- End-of-day processing jobs");
        logger.LogInformation("- On-demand portfolio refresh operations");
        logger.LogInformation("- Automated revaluation with external price feeds");
    }
}