using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;


namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for fetching market prices using EOD Historical Data
/// </summary>
public class PriceFetchingService : IPriceFetching
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly ILogger<PriceFetchingService> _logger;
    private readonly EodApiOptions _eodApiOptions;

    public PriceFetchingService(
        IHoldingRepository holdingRepository,
        ILogger<PriceFetchingService> logger,
        IOptions<EodApiOptions> eodApiOptions)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eodApiOptions = eodApiOptions?.Value ?? throw new ArgumentNullException(nameof(eodApiOptions));
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
            
            // Validate API token is configured
            if (string.IsNullOrWhiteSpace(_eodApiOptions.Token))
            {
                _logger.LogError("EOD API token is not configured. Please set EodApi:Token in configuration.");
                throw new InvalidOperationException("EOD API token is not configured.");
            }
            
            // Use direct HTTP call to EOD API endpoint like your working example
            // Format: https://eodhd.com/api/eod/SYMBOL.EXCHANGE?api_token=TOKEN&fmt=json&order=d
            var symbol = ticker.Contains('.') ? ticker : $"{ticker}.LSE"; // Default to LSE if no exchange specified
            var url = $"{_eodApiOptions.BaseUrl}/eod/{symbol}?api_token={_eodApiOptions.Token}&fmt=json&order=d&period=d";
            
            _logger.LogInformation("Calling EOD API: {Url}", url.Replace(_eodApiOptions.Token, "***"));
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("EOD API returned {StatusCode} for {Ticker}: {Error}", 
                        response.StatusCode, ticker, errorContent);
                    return null;
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("EOD API response for {Ticker}: {Response}", ticker, jsonContent);
                
                // Parse the JSON response
                var priceData = JsonSerializer.Deserialize<EODHistoricalData[]>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (priceData == null || !priceData.Any())
                {
                    _logger.LogWarning("No price data returned from EOD API for ISIN {ISIN} (Ticker: {Ticker})", isin, ticker);
                    return null;
                }
                
                // Get the most recent price data
                var latestPrice = priceData.First(); // Data is ordered by date descending (order=d)
                
                // Map the EOD data to our InstrumentPriceData
                return new InstrumentPriceData
                {
                    ISIN = isin,
                    Price = latestPrice.Close,
                    Currency = "USD", // EOD typically returns USD for most stocks
                    Symbol = ticker,
                    Name = $"Instrument {isin}",
                    Change = latestPrice.Close - latestPrice.Open,
                    ChangePercent = latestPrice.Open != 0 ? ((latestPrice.Close - latestPrice.Open) / latestPrice.Open) * 100 : null,
                    PreviousClose = latestPrice.Open,
                    Open = latestPrice.Open,
                    High = latestPrice.High,
                    Low = latestPrice.Low,
                    Volume = latestPrice.Volume,
                    Market = symbol.Contains('.') ? symbol.Split('.').Last() : "LSE",
                    MarketStatus = "Closed",
                    Timestamp = DateTime.Parse(latestPrice.Date)
                };
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error while fetching price for {Ticker}: {Message}", ticker, httpEx.Message);
                throw;
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "Timeout while fetching price for {Ticker}", ticker);
                throw;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error for {Ticker}: {Message}", ticker, jsonEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching price for ISIN {ISIN} (Ticker: {Ticker})", isin, ticker);
            throw;
        }
    }

    // Data model for EOD Historical Data API response
    private class EODHistoricalData
    {
        public string Date { get; set; } = string.Empty;
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Adjusted_Close { get; set; }
        public long Volume { get; set; }
    }
}