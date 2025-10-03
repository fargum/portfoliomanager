using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using EOD;
using EOD.Model;
using static EOD.API;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for fetching market prices using EOD Historical Data
/// </summary>
public class PriceFetchingService : IPriceFetching
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly ILogger<PriceFetchingService> _logger;

    public PriceFetchingService(
        IHoldingRepository holdingRepository,
        ILogger<PriceFetchingService> logger)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PriceFetchResult> FetchPricesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new PriceFetchResult
        {
            FetchedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting price fetch operation for holdings on date {ValuationDate}", valuationDate);

            // Step 1: Get distinct ISINs from holdings for the specified date
            var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var isins = await _holdingRepository.GetDistinctIsinsByDateAsync(dateTime, cancellationToken);
            var isinList = isins.ToList();

            // TODO: Remove this test line - just for testing
            // var isinSet = new HashSet<string>{"AAPL.US"};

            result.TotalIsins = isinList.Count;

            if (!isinList.Any())
            {
                _logger.LogWarning("No ISINs found for date {ValuationDate}", valuationDate);
                result.FetchDuration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("Found {Count} distinct ISINs for date {ValuationDate}. Fetching prices...", 
                isinList.Count, valuationDate);

            // Step 2: Fetch prices for each ISIN using EOD Historical Data
            var priceTasks = isinList.Select(async isin =>
            {
                try
                {
                    // Use the EOD Historical Data client to fetch real-time price
                    // Note: You may need to adjust this based on the exact API methods available
                    var priceData = await FetchPriceForIsin(isin, cancellationToken);
                    
                    if (priceData != null)
                    {
                        result.Prices.Add(priceData);
                        _logger.LogDebug("Successfully fetched price for ISIN {ISIN}: {Price} {Currency}", 
                            isin, priceData.Price, priceData.Currency);
                    }
                    else
                    {
                        result.FailedIsins.Add(new FailedPriceData
                        {
                            ISIN = isin,
                            ErrorMessage = "No price data available",
                            ErrorCode = "NO_DATA"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch price for ISIN {ISIN}", isin);
                    result.FailedIsins.Add(new FailedPriceData
                    {
                        ISIN = isin,
                        ErrorMessage = ex.Message,
                        ErrorCode = "FETCH_ERROR"
                    });
                }
            });

            // Execute all price fetching tasks concurrently
            await Task.WhenAll(priceTasks);

            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;

            _logger.LogInformation("Price fetch operation completed. Success: {SuccessCount}, Failed: {FailedCount}, Duration: {Duration}ms",
                result.SuccessfulPrices, result.FailedPrices, result.FetchDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;
            _logger.LogError(ex, "Error during price fetch operation for date {ValuationDate}", valuationDate);
            throw;
        }
    }

    private async Task<InstrumentPriceData?> FetchPriceForIsin(string isin, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching price for ISIN {ISIN}", isin);
            
            // TODO: Implement actual EOD Historical Data API integration
            // The EOD API class doesn't have a GetPriceAsync method for ISINs directly
            // You would need to use the correct EOD API methods, such as:
            var apiToken = "demo";
            var api = new API(apiToken);
            // var realTimeData = await api.GetRealTimeDataAsync(symbol);
            // var fundamentalData = await api.GetFundamentalDataAsync(symbol);
            
            // For now, simulate API call and return placeholder data
            await Task.Delay(100, cancellationToken);

            // EOD API call to get historical stock price data
            List<HistoricalStockPrice>? response = await api.GetEndOfDayHistoricalStockPriceAsync("AAPL.US", new DateTime(2025, 10, 1), new DateTime(2025, 10, 1), HistoricalPeriod.Daily);

            // Check if we got any data back
            if (response == null || !response.Any())
            {
                _logger.LogWarning("No price data returned from EOD API for ISIN {ISIN}", isin);
                return null;
            }

            // Get the last (most recent) price from the response
            var latestPrice = response.Last();

            // Map the EOD HistoricalStockPrice data to our InstrumentPriceData
            return new InstrumentPriceData
            {
                ISIN = isin,
                Price = (decimal)(latestPrice.Close ?? 0),
                Currency = "USD", // EOD typically returns USD, adjust if needed
                Symbol = "AAPL.US", // You may want to pass this as a parameter or derive it from ISIN
                Name = $"Instrument {isin}", // You might want to get this from fundamental data
                Change = (decimal?)((latestPrice.Close ?? 0) - (latestPrice.Open ?? 0)), // Daily change
                ChangePercent = latestPrice.Open.HasValue && latestPrice.Open != 0 && latestPrice.Close.HasValue 
                    ? (decimal?)(((latestPrice.Close.Value - latestPrice.Open.Value) / latestPrice.Open.Value) * 100) 
                    : null,
                PreviousClose = (decimal?)(latestPrice.Open ?? 0), // Using open as previous close for daily data
                Open = (decimal?)(latestPrice.Open ?? 0),
                High = (decimal?)(latestPrice.High ?? 0),
                Low = (decimal?)(latestPrice.Low ?? 0),
                Volume = latestPrice.Volume,
                Market = "NYSE", // You might want to derive this from the symbol or ISIN
                MarketStatus = "Closed", // Historical data implies market is closed for that day
                Timestamp = latestPrice.Date
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching price for ISIN {ISIN}", isin);
            throw;
        }
    }
}