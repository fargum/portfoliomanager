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

            // Step 1: Get distinct instruments (ISIN + Ticker) from holdings for the specified date
            var dateTime = DateTime.SpecifyKind(valuationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var instruments = await _holdingRepository.GetDistinctInstrumentsByDateAsync(dateTime, cancellationToken);
            var instrumentList = instruments.ToList();

            result.TotalIsins = instrumentList.Count;

            if (!instrumentList.Any())
            {
                _logger.LogWarning("No instruments found for date {ValuationDate}", valuationDate);
                result.FetchDuration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("Found {Count} distinct instruments for date {ValuationDate}. Fetching prices...", 
                instrumentList.Count, valuationDate);

            // Step 2: Fetch prices for each instrument using EOD Historical Data
            var priceTasks = instrumentList.Select(async instrument =>
            {
                try
                {
                    // Use the EOD Historical Data client to fetch real-time price
                    var priceData = await FetchPriceForInstrument(instrument.ISIN, instrument.Ticker, valuationDate, cancellationToken);
                    
                    if (priceData != null)
                    {
                        result.Prices.Add(priceData);
                        _logger.LogDebug("Successfully fetched price for ISIN {ISIN} (Ticker: {Ticker}): {Price} {Currency}", 
                            instrument.ISIN, instrument.Ticker, priceData.Price, priceData.Currency);
                    }
                    else
                    {
                        result.FailedIsins.Add(new FailedPriceData
                        {
                            ISIN = instrument.ISIN,
                            ErrorMessage = "No price data available",
                            ErrorCode = "NO_DATA"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch price for ISIN {ISIN} (Ticker: {Ticker})", instrument.ISIN, instrument.Ticker);
                    result.FailedIsins.Add(new FailedPriceData
                    {
                        ISIN = instrument.ISIN,
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

    private async Task<InstrumentPriceData?> FetchPriceForInstrument(string isin, string? ticker, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching price for ISIN {ISIN} with ticker {Ticker} for date {ValuationDate}", isin, ticker, valuationDate);
            
            // Skip instruments without ticker symbols as EOD API requires tickers
            if (string.IsNullOrWhiteSpace(ticker))
            {
                _logger.LogWarning("No ticker available for ISIN {ISIN}, skipping price fetch", isin);
                return null;
            }
            
            var apiToken = "demo";
            var api = new API(apiToken);
                       
            // Convert DateOnly to DateTime for the EOD API call
            var targetDate = valuationDate.ToDateTime(TimeOnly.MinValue);
            
            List<HistoricalStockPrice>? response = await api.GetEndOfDayHistoricalStockPriceAsync(ticker, targetDate, targetDate, HistoricalPeriod.Daily);

            // Check if we got any data back
            if (response == null || !response.Any())
            {
                _logger.LogWarning("No price data returned from EOD API for ISIN {ISIN} (Ticker: {Ticker}) on date {ValuationDate}", isin, ticker, valuationDate);
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
                Symbol = ticker, // Use the actual ticker symbol we queried
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
                Market = "LSE", // You might want to derive this from the symbol or ISIN
                MarketStatus = "Closed", // Historical data implies market is closed for that day
                Timestamp = latestPrice.Date
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching price for ISIN {ISIN} (Ticker: {Ticker})", isin, ticker);
            throw;
        }
    }
}