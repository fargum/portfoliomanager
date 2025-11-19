using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Constants;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for fetching market prices using EOD Data
/// </summary>
public class PriceFetchingService : IPriceFetching, IDisposable
{
    private readonly IHoldingRepository _holdingRepository;
    private readonly IInstrumentPriceRepository _instrumentPriceRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PriceFetchingService> _logger;
    private readonly EodApiOptions _eodApiOptions;
    private readonly SemaphoreSlim _dbSemaphore;

    public PriceFetchingService(
        IHoldingRepository holdingRepository,
        IInstrumentPriceRepository instrumentPriceRepository,
        IExchangeRateRepository exchangeRateRepository,
        IUnitOfWork unitOfWork,
        ILogger<PriceFetchingService> logger,
        IOptions<EodApiOptions> eodApiOptions)
    {
        _holdingRepository = holdingRepository ?? throw new ArgumentNullException(nameof(holdingRepository));
        _instrumentPriceRepository = instrumentPriceRepository ?? throw new ArgumentNullException(nameof(instrumentPriceRepository));
        _exchangeRateRepository = exchangeRateRepository ?? throw new ArgumentNullException(nameof(exchangeRateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eodApiOptions = eodApiOptions?.Value ?? throw new ArgumentNullException(nameof(eodApiOptions));
        _dbSemaphore = new SemaphoreSlim(1, 1); // Allow only 1 concurrent database operation
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
                        // No price data available from EOD - try to roll forward previous price
                        _logger.LogInformation("No current price available for {Ticker}, attempting to roll forward previous price", instrument.Ticker);
                        
                        // Use semaphore to ensure thread-safe database access
                        await _dbSemaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var latestPrice = await _instrumentPriceRepository.GetLatestPriceAsync(instrument.InstrumentId, valuationDate.AddDays(-1), cancellationToken);
                            
                            if (latestPrice != null)
                            {
                                // Roll forward the previous price with updated valuation date
                                var rolledForwardPrice = new InstrumentPrice
                                {
                                    InstrumentId = instrument.InstrumentId,
                                    Ticker = latestPrice.Ticker,
                                    ValuationDate = valuationDate, // Use current valuation date
                                    Name = latestPrice.Name,
                                    Price = latestPrice.Price, // Keep same price
                                    Currency = latestPrice.Currency,
                                    Change = 0, // No change since we're rolling forward
                                    ChangePercent = 0, // No change percent
                                    PreviousClose = latestPrice.Price, // Previous close is the same price
                                    Open = latestPrice.Price, // Open is the same price
                                    High = latestPrice.Price, // High is the same price
                                    Low = latestPrice.Price, // Low is the same price
                                    Volume = 0, // No volume for rolled forward price
                                    Market = latestPrice.Market,
                                    MarketStatus = "ROLLED_FORWARD", // Indicate this is a rolled forward price
                                    PriceTimestamp = DateTime.UtcNow,
                                    CreatedAt = DateTime.UtcNow
                                };
                                
                                lock (pricesToPersist)
                                {
                                    pricesToPersist.Add(rolledForwardPrice);
                                }
                                
                                _logger.LogInformation("Successfully rolled forward price for {Ticker}: {Price} {Currency} from {PreviousDate}", 
                                    instrument.Ticker, latestPrice.Price, latestPrice.Currency, latestPrice.ValuationDate);
                            }
                            else
                            {
                                // No previous price available either
                                _logger.LogWarning("No previous price available to roll forward for {Ticker}", instrument.Ticker);
                                result.FailedTickers.Add(new FailedPriceData
                                {
                                    Ticker = instrument.Ticker,
                                    ErrorMessage = "No price data available and no previous price to roll forward",
                                    ErrorCode = "NO_DATA"
                                });
                            }
                        }
                        finally
                        {
                            _dbSemaphore.Release();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch price for {Ticker}, attempting to roll forward previous price", instrument.Ticker);
                    
                    // Try to roll forward previous price when API call fails
                    try
                    {
                        var latestPrice = await _instrumentPriceRepository.GetLatestPriceAsync(instrument.InstrumentId, valuationDate.AddDays(-1), cancellationToken);
                        
                        if (latestPrice != null)
                        {
                            // Roll forward the previous price with updated valuation date
                            var rolledForwardPrice = new InstrumentPrice
                            {
                                InstrumentId = instrument.InstrumentId,
                                Ticker = latestPrice.Ticker,
                                ValuationDate = valuationDate, // Use current valuation date
                                Name = latestPrice.Name,
                                Price = latestPrice.Price, // Keep same price
                                Currency = latestPrice.Currency,
                                Change = 0, // No change since we're rolling forward
                                ChangePercent = 0, // No change percent
                                PreviousClose = latestPrice.Price, // Previous close is the same price
                                Open = latestPrice.Price, // Open is the same price
                                High = latestPrice.Price, // High is the same price
                                Low = latestPrice.Price, // Low is the same price
                                Volume = 0, // No volume for rolled forward price
                                Market = latestPrice.Market,
                                MarketStatus = "ROLLED_FORWARD", // Indicate this is a rolled forward price
                                PriceTimestamp = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            lock (pricesToPersist)
                            {
                                pricesToPersist.Add(rolledForwardPrice);
                            }
                            
                            _logger.LogInformation("Successfully rolled forward price for {Ticker} after API failure: {Price} {Currency} from {PreviousDate}", 
                                instrument.Ticker, latestPrice.Price, latestPrice.Currency, latestPrice.ValuationDate);
                        }
                        else
                        {
                            // No previous price available either
                            _logger.LogWarning("No previous price available to roll forward for {Ticker} after API failure", instrument.Ticker);
                            result.FailedTickers.Add(new FailedPriceData
                            {
                                Ticker = instrument.Ticker,
                                ErrorMessage = $"API fetch failed: {ex.Message}. No previous price to roll forward.",
                                ErrorCode = "FETCH_ERROR"
                            });
                        }
                    }
                    catch (Exception rollforwardEx)
                    {
                        _logger.LogError(rollforwardEx, "Failed to roll forward price for {Ticker}", instrument.Ticker);
                        result.FailedTickers.Add(new FailedPriceData
                        {
                            Ticker = instrument.Ticker,
                            ErrorMessage = $"API fetch failed: {ex.Message}. Rollforward failed: {rollforwardEx.Message}",
                            ErrorCode = "FETCH_ERROR"
                        });
                    }
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

            // Step 3: Fetch and persist exchange rates for the same valuation date
            _logger.LogInformation("Fetching exchange rates for valuation date {ValuationDate}", valuationDate);
            try
            {
                var exchangeRateResult = await FetchAndPersistExchangeRatesForDateAsync(valuationDate, cancellationToken);
                _logger.LogInformation("Exchange rate fetch completed: Success: {SuccessfulRates}, Rolled Forward: {RolledForwardRates}, Failed: {FailedRates}, Duration: {Duration}ms",
                    exchangeRateResult.SuccessfulRates, exchangeRateResult.RolledForwardRates, exchangeRateResult.FailedRates, exchangeRateResult.FetchDuration.TotalMilliseconds);
            }
            catch (Exception exchangeEx)
            {
                _logger.LogWarning(exchangeEx, "Failed to fetch exchange rates for {ValuationDate}, but price fetching was successful", valuationDate);
                // Continue - don't fail the entire operation if exchange rates fail
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
            
            // Use direct HTTP call to EOD API endpoint for historical data on specific date
            // Format: https://eodhd.com/api/eod/SYMBOL.EXCHANGE?api_token=TOKEN&fmt=json&from=YYYY-MM-DD&to=YYYY-MM-DD
            var symbol = ticker.Contains('.') ? ticker : $"{ticker}.LSE"; // Default to LSE if no exchange specified
            var dateString = valuationDate.ToString("yyyy-MM-dd");
            var url = $"{_eodApiOptions.BaseUrl}/eod/{symbol}?api_token={_eodApiOptions.Token}&fmt=json&from={dateString}&to={dateString}";
            
            _logger.LogInformation("Calling EOD API for specific date {ValuationDate}: {Url}", valuationDate, url.Replace(_eodApiOptions.Token, "***"));
            
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
                    _logger.LogWarning("No price data returned from EOD API for ISIN {ISIN} (Ticker: {Ticker}) on date {ValuationDate}. This may be a non-trading day.", 
                        isin, ticker, valuationDate);
                    return null;
                }
                
                // Get the price data for the requested date (should be only one record when using from/to date range)
                var requestedDatePrice = priceData.FirstOrDefault(p => 
                    DateTime.Parse(p.Date).Date == valuationDate.ToDateTime(TimeOnly.MinValue).Date);
                
                
if (requestedDatePrice == null)
                {
                    _logger.LogWarning("No price data available for the specific date {ValuationDate} for {Ticker}. Available dates: {AvailableDates}",
                        valuationDate, ticker, string.Join(", ", priceData.Select(p => p.Date)));
                    return null;
                }
                
                // Map the EOD data to our InstrumentPriceData
                return new InstrumentPriceData
                {
                    Ticker = ticker,
                    Price = requestedDatePrice.Close,
                    Currency = CurrencyConstants.USD, // EOD typically returns USD for most stocks
                    Symbol = ticker,
                    Name = $"Instrument {ticker}",
                    Change = requestedDatePrice.Close - requestedDatePrice.Open,
                    ChangePercent = requestedDatePrice.Open != 0 ? ((requestedDatePrice.Close - requestedDatePrice.Open) / requestedDatePrice.Open) * 100 : null,
                    PreviousClose = requestedDatePrice.Open,
                    Open = requestedDatePrice.Open,
                    High = requestedDatePrice.High,
                    Low = requestedDatePrice.Low,
                    Volume = requestedDatePrice.Volume,
                    Market = symbol.Contains('.') ? symbol.Split('.').Last() : "LSE",
                    MarketStatus = "Closed",
                    Timestamp = DateTime.Parse(requestedDatePrice.Date)
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

    public async Task<ExchangeRateFetchResult> FetchAndPersistExchangeRatesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExchangeRateFetchResult
        {
            FetchedAt = DateTime.UtcNow
        };

        var ratesToPersist = new List<ExchangeRate>();

        try
        {
            _logger.LogInformation("Starting exchange rate fetch and persist operation for valuation date {ValuationDate}", valuationDate);

            // Define required currency pairs (base currency is always GBP)
            var requiredCurrencyPairs = new[]
            {
                (CurrencyConstants.USD, CurrencyConstants.GBP),
                (CurrencyConstants.EUR, CurrencyConstants.GBP)
            };

            result.TotalCurrencyPairs = requiredCurrencyPairs.Length;

            // Fetch exchange rates for each currency pair
            var rateTasks = requiredCurrencyPairs.Select(async currencyPair =>
            {
                try
                {
                    var (baseCurrency, targetCurrency) = currencyPair;
                    var rateData = await FetchExchangeRateForPair(baseCurrency, targetCurrency, valuationDate, cancellationToken);
                    
                    if (rateData != null)
                    {
                        var exchangeRate = new ExchangeRate(
                            baseCurrency,
                            targetCurrency,
                            rateData.Value.rate,
                            valuationDate,
                            "EOD"
                        );
                        
                        lock (ratesToPersist)
                        {
                            ratesToPersist.Add(exchangeRate);
                        }
                        
                        _logger.LogDebug("Successfully fetched exchange rate for {BaseCurrency}/{TargetCurrency}: {Rate}", 
                            baseCurrency, targetCurrency, rateData.Value.rate);
                    }
                    else
                    {
                        // Try to roll forward previous rate
                        await TryRollForwardExchangeRate(baseCurrency, targetCurrency, valuationDate, ratesToPersist, result, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch exchange rate for {CurrencyPair}, attempting rollforward", $"{currencyPair.Item1}/{currencyPair.Item2}");
                    
                    // Try to roll forward previous rate when API fails
                    await TryRollForwardExchangeRate(currencyPair.Item1, currencyPair.Item2, valuationDate, ratesToPersist, result, cancellationToken);
                }
            });

            // Execute all exchange rate fetching tasks concurrently
            await Task.WhenAll(rateTasks);

            // Persist the collected rates to database
            if (ratesToPersist.Any())
            {
                _logger.LogInformation("Persisting {Count} exchange rate records to database for valuation date {ValuationDate}", 
                    ratesToPersist.Count, valuationDate);
                
                await _exchangeRateRepository.BulkUpsertAsync(ratesToPersist, cancellationToken);
                await _unitOfWork.SaveChangesAsync();
                
                _logger.LogInformation("Successfully persisted {Count} exchange rate records to database", ratesToPersist.Count);
            }

            result.SuccessfulRates = ratesToPersist.Count(r => r.Source == "EOD");
            result.RolledForwardRates = ratesToPersist.Count(r => r.Source == "ROLLED_FORWARD");
            result.FailedRates = result.TotalCurrencyPairs - result.SuccessfulRates - result.RolledForwardRates;

            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;

            _logger.LogInformation("Exchange rate fetch completed for {ValuationDate}. Success: {SuccessfulRates}, Rolled Forward: {RolledForwardRates}, Failed: {FailedRates}, Duration: {Duration}ms",
                valuationDate, result.SuccessfulRates, result.RolledForwardRates, result.FailedRates, result.FetchDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;
            _logger.LogError(ex, "Error during exchange rate fetch operation for date {ValuationDate}", valuationDate);
            throw;
        }
    }

    private async Task<(decimal rate, DateTime timestamp)?> FetchExchangeRateForPair(string baseCurrency, string targetCurrency, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_eodApiOptions.Token))
        {
            throw new InvalidOperationException("EOD API token is not configured.");
        }

        try
        {
            // EOD Forex endpoint format with date filter: https://eodhd.com/api/eod/USDGBP.FOREX?api_token=TOKEN&fmt=json&from=2023-01-01&to=2023-01-01
            var forexSymbol = $"{baseCurrency}{targetCurrency}.FOREX";
            var dateString = valuationDate.ToString("yyyy-MM-dd");
            var url = $"{_eodApiOptions.BaseUrl}/eod/{forexSymbol}?api_token={_eodApiOptions.Token}&fmt=json&from={dateString}&to={dateString}";

            _logger.LogInformation("Fetching exchange rate for {BaseCurrency}/{TargetCurrency} for date {ValuationDate} from URL: {Url}", 
                baseCurrency, targetCurrency, valuationDate, url.Replace(_eodApiOptions.Token, "***"));

            using var httpClient = new HttpClient();
            
httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);

            var response = await httpClient.GetStringAsync(url, cancellationToken);
            _logger.LogInformation("EOD FOREX API response for {BaseCurrency}/{TargetCurrency}: {Response}", 
                baseCurrency, targetCurrency, response.Length > 500 ? response.Substring(0, 500) + "..." : response);
            
            var forexData = JsonSerializer.Deserialize<EODHistoricalData[]>(response);

            if (forexData != null && forexData.Length > 0)
            {
                var latestRate = forexData[0]; // Should be the rate for the specific date
                _logger.LogInformation("Successfully parsed exchange rate for {BaseCurrency}/{TargetCurrency}: {Rate} on {Date}", 
                    baseCurrency, targetCurrency, latestRate.Close, latestRate.Date);
                return (latestRate.Close, DateTime.Parse(latestRate.Date));
            }

            _logger.LogWarning("No forex data returned from EOD API for {BaseCurrency}/{TargetCurrency} on {ValuationDate}", baseCurrency, targetCurrency, valuationDate);
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error while fetching exchange rate for {BaseCurrency}/{TargetCurrency}: {Message}", baseCurrency, targetCurrency, httpEx.Message);
            throw;
        }
        catch (TaskCanceledException timeoutEx)
        {
            _logger.LogError(timeoutEx, "Timeout while fetching exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            throw;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON parsing error while fetching exchange rate for {BaseCurrency}/{TargetCurrency}: {Message}", baseCurrency, targetCurrency, jsonEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            throw;
        }
    }

    private async Task TryRollForwardExchangeRate(string baseCurrency, string targetCurrency, DateOnly valuationDate, List<ExchangeRate> ratesToPersist, ExchangeRateFetchResult result, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("No current exchange rate available for {BaseCurrency}/{TargetCurrency}, attempting to roll forward previous rate", baseCurrency, targetCurrency);
            
            var latestRate = await _exchangeRateRepository.GetLatestRateAsync(baseCurrency, targetCurrency, valuationDate.AddDays(-1), cancellationToken);
            
            if (latestRate != null)
            {
                // Roll forward the previous rate with updated valuation date
                var rolledForwardRate = new ExchangeRate(
                    baseCurrency,
                    targetCurrency,
                    latestRate.Rate, // Keep same rate
                    valuationDate, // Use current valuation date
                    "ROLLED_FORWARD"
                );
                
                lock (ratesToPersist)
                {
                    ratesToPersist.Add(rolledForwardRate);
                }
                
                _logger.LogInformation("Successfully rolled forward exchange rate for {BaseCurrency}/{TargetCurrency}: {Rate} from {PreviousDate}", 
                    baseCurrency, targetCurrency, latestRate.Rate, latestRate.RateDate);
            }
            else
            {
                // No previous rate available either
                _logger.LogWarning("No previous exchange rate available to roll forward for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
                result.FailedCurrencyPairs.Add(new FailedExchangeRateData
                {
                    BaseCurrency = baseCurrency,
                    TargetCurrency = targetCurrency,
                    ErrorMessage = "No exchange rate data available and no previous rate to roll forward",
                    ErrorCode = "NO_DATA"
                });
            }
        }
        catch (Exception rollforwardEx)
        {
            _logger.LogError(rollforwardEx, "Failed to roll forward exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            result.FailedCurrencyPairs.Add(new FailedExchangeRateData
            {
                BaseCurrency = baseCurrency,
                TargetCurrency = targetCurrency,
                ErrorMessage = $"Rollforward failed: {rollforwardEx.Message}",
                ErrorCode = "ROLLFORWARD_ERROR"
            });
        }
    }

    // Data model for EOD Historical Data API response
    private class EODHistoricalData
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
        
        [JsonPropertyName("open")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("high")]
        public decimal High { get; set; }
        
        [JsonPropertyName("low")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("close")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("adjusted_close")]
        public decimal Adjusted_Close { get; set; }
        
        [JsonPropertyName("volume")]
        public long Volume { get; set; }
    }

    public void Dispose()
    {
        _dbSemaphore?.Dispose();
    }
}