using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Entities;
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
    private readonly IInstrumentPriceRepository _instrumentPriceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PriceFetchingService> _logger;
    private readonly EodApiOptions _eodApiOptions;

    public PriceFetchingService(
        IHoldingRepository holdingRepository,
        IInstrumentPriceRepository instrumentPriceRepository,
        IUnitOfWork unitOfWork,
        ILogger<PriceFetchingService> logger,
        IOptions<EodApiOptions> eodApiOptions)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _instrumentPriceRepository = instrumentPriceRepository ?? throw new ArgumentNullException(nameof(instrumentPriceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eodApiOptions = eodApiOptions?.Value ?? throw new ArgumentNullException(nameof(eodApiOptions));
    }

    public async Task<PriceFetchResult> FetchAndPersistPricesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new PriceFetchResult
        {
            FetchedAt = DateTime.UtcNow
        };

        var pricesToPersist = new List<InstrumentPrice>();

        try
        {
            _logger.LogInformation("Starting price fetch and persist operation for valuation date {ValuationDate}", valuationDate);

            // Step 1: Get distinct instruments (ISIN + Ticker) from all holdings regardless of date
            var instruments = await _holdingRepository.GetAllDistinctInstrumentsAsync(cancellationToken);
            var instrumentList = instruments.ToList();

            result.TotalTickers= instrumentList.Count;

            if (!instrumentList.Any())
            {
                _logger.LogWarning("No instruments found in holdings database");
                result.FetchDuration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("Found {Count} distinct instruments from all holdings. Fetching prices for valuation date {ValuationDate}...", 
                instrumentList.Count, valuationDate);

            // Step 2: Fetch prices for each instrument using EOD Historical Data
            var priceTasks = instrumentList.Select(async instrument =>
            {
                try
                {
                    // Use the EOD Historical Data client to fetch real-time price
                    var priceData = await FetchPriceForInstrument(instrument.Ticker, instrument.Ticker, valuationDate, cancellationToken);
                    
                    if (priceData != null)
                    {
                        // Convert to InstrumentPrice entity for persistence
                        var instrumentPrice = new InstrumentPrice
                        {
                            InstrumentId = instrument.InstrumentId,
                            Ticker = priceData.Ticker,
                            ValuationDate = valuationDate,
                            Name = priceData.Name,
                            Price = priceData.Price,
                            Currency = priceData.Currency,
                            Change = priceData.Change,
                            ChangePercent = priceData.ChangePercent,
                            PreviousClose = priceData.PreviousClose,
                            Open = priceData.Open,
                            High = priceData.High,
                            Low = priceData.Low,
                            Volume = priceData.Volume,
                            Market = priceData.Market,
                            MarketStatus = priceData.MarketStatus,
                            PriceTimestamp = priceData.Timestamp?.Kind == DateTimeKind.Utc 
                                ? priceData.Timestamp 
                                : priceData.Timestamp?.ToUniversalTime()
                        };
                        
                        // Set audit timestamps
                        instrumentPrice.CreatedAt = DateTime.UtcNow;
                        
                        lock (pricesToPersist)
                        {
                            pricesToPersist.Add(instrumentPrice);
                        }
                        
                        _logger.LogDebug("Successfully fetched price for (Ticker: {Ticker}): {Price} {Currency}", 
                            instrument.Ticker, priceData.Price, priceData.Currency);
                    }
                    else
                    {
                        result.FailedTickers.Add(new FailedPriceData
                        {
                            Ticker = instrument.Ticker,
                            ErrorMessage = "No price data available",
                            ErrorCode = "NO_DATA"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch price for (Ticker: {Ticker})", instrument.Ticker);
                    result.FailedTickers.Add(new FailedPriceData
                    {
                        Ticker = instrument.Ticker,
                        ErrorMessage = ex.Message,
                        ErrorCode = "FETCH_ERROR"
                    });
                }
            });

            // Execute all price fetching tasks concurrently
            await Task.WhenAll(priceTasks);

            // Persist the collected prices to database
            if (pricesToPersist.Any())
            {
                _logger.LogInformation("Persisting {Count} price records to database for valuation date {ValuationDate}", 
                    pricesToPersist.Count, valuationDate);
                
                await _instrumentPriceRepository.BulkUpsertAsync(pricesToPersist, cancellationToken);
                await _unitOfWork.SaveChangesAsync();
                
                _logger.LogInformation("Successfully persisted {Count} price records to database", pricesToPersist.Count);
            }

            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;

            // Update result with counts (but no actual price data since it's now persisted)
            result.SuccessfulPrices = pricesToPersist.Count;

            _logger.LogInformation("Price fetch and persist operation completed. Success: {SuccessCount}, Failed: {FailedCount}, Duration: {Duration}ms",
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
                    Ticker = ticker,
                    Price = latestPrice.Close,
                    Currency = "USD", // EOD typically returns USD for most stocks
                    Symbol = ticker,
                    Name = $"Instrument {ticker}",
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